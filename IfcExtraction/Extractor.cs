using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.WindowsAzure.Storage.Blob;
using Xbim.Common;
using Xbim.Common.Metadata;
using Xbim.Ifc4.Interfaces;
using Xbim.IO.Memory;
using XbimCloudCommon;

namespace IfcExtraction
{
    internal class Extractor
    {
        private bool _includeGeometry;

        public void ExtractData(
            XbimCloudModel blobInfo,
            CloudBlockBlob input,
            CloudBlockBlob state,
            CloudBlockBlob output)
        {
            if (input == null || output == null)
            {
                state.WriteLine("Error: You have to specify input and output file.");
                return;
            }

            state.WriteLine("Processing started");
            var w = Stopwatch.StartNew();
            long originalCount;
            long resultCount;

            _includeGeometry = blobInfo.CreateGeometry;

            IEntityFactory factory = null;
            using (var headerStream = input.OpenRead())
            {
                var header = MemoryModel.GetStepFileHeader(headerStream);
                var schema = header.FileSchema.Schemas.First().ToUpper();
                if (schema == "IFC2X3")
                    factory = new Xbim.Ifc2x3.EntityFactory();
                if (schema == "IFC4")
                    factory = new Xbim.Ifc4.EntityFactory();
                if (factory == null)
                {
                    state.WriteLine("Unidentified schema " + schema + ". Only IFC2X3 and IFC4 are supported.");
                    return;
                }

                state.WriteLine("Identified schema version: " + schema);
            }


            using (var inputStream = input.OpenRead())
            {
                using (var source = new MemoryModel(factory))
                {
                    source.LoadStep21(inputStream);
                    using (var target = new MemoryModel(factory))
                    {
                        //insertion itself will be configured to happen out of transaction but other operations might need to be transactional
                        using (var txn = target.BeginTransaction("COBie data extraction"))
                        {
                            var toInsert = GetEntitiesToInsert(source, blobInfo.Ids);
                            var cache = new Dictionary<int, IPersistEntity>();

                            //set to happen out of transaction. This should save some memory used by transaction log
                            foreach (var entity in toInsert)
                                target.InsertCopy(entity, cache, false, true, Filter, true);

                            txn.Commit();
                        }

                        var ext = blobInfo.Extension2;
                        using (var file = output.OpenWrite())
                        {
                            if (ext.ToLower() == ".ifcxml")
                                target.SaveAsXml(file, new XmlWriterSettings { Indent = true });
                            else
                                target.SaveAsStep21(file);
                        }
                        originalCount = source.Instances.Count;
                        resultCount = target.Instances.Count;
                    }
                }
            }

            w.Stop();

            var processingTime = w.ElapsedMilliseconds;
            var msg = string.Format("COBie content extracted in {0}s <br />" +
                                    "Number of objects in original: {1:n0} <br />" +
                                    "Number of objects in result: {2:n0}",
                processingTime / 1000,
                originalCount,
                resultCount);
            state.WriteLine(msg);
        }

        private IEnumerable<IPersistEntity> GetEntitiesToInsert(IModel model, string guids)
        {
            if (string.IsNullOrWhiteSpace(guids))
                return model.Instances.OfType<IIfcRoot>();

            var ids =
                guids
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim()).ToList();

            List<IPersistEntity> roots;
            if (GetLabel(ids.First()) < 0)
                //root elements specified by GUID
                roots = model.Instances.Where<IIfcRoot>(r => ids.Contains(r.GlobalId.ToString())).Cast<IPersistEntity>().ToList();
            else
            {
                var labels = ids.Select(GetLabel).ToList();
                //root elements specified by GUID
                roots = model.Instances.Where<IIfcRoot>(r => labels.Contains(r.EntityLabel)).Cast<IPersistEntity>().ToList();
            }

            _primaryElements = roots.OfType<IIfcProduct>().ToList();

            //add any aggregated elements. For example IfcRoof is typically aggregation of one or more slabs so we need to bring
            //them along to have all the information both for geometry and for properties and materials.
            //This has to happen before we add spatial hierarchy or it would bring in full hierarchy which is not an intention
            var decompositionRels = GetAggregations(_primaryElements.ToList(), model).ToList();
            _primaryElements.AddRange(_decomposition);
            roots.AddRange(decompositionRels);

            //we should add spatial hierarchy right here so it brings its attributes as well
            var spatialRels = model.Instances.Where<IIfcRelContainedInSpatialStructure>(
                r => _primaryElements.Any(e => r.RelatedElements.Contains(e))).ToList();
            var spatialRefs =
                model.Instances.Where<IIfcRelReferencedInSpatialStructure>(
                    r => _primaryElements.Any(e => r.RelatedElements.Contains(e))).ToList();
            var bottomSpatialHierarchy =
                spatialRels.Select(r => r.RelatingStructure).Union(spatialRefs.Select(r => r.RelatingStructure)).ToList();
            var spatialAggregations = GetUpstreamHierarchy(bottomSpatialHierarchy, model).ToList();

            //add all spatial elements from bottom and from upstream hierarchy
            _primaryElements.AddRange(bottomSpatialHierarchy);
            _primaryElements.AddRange(spatialAggregations.Select(r => r.RelatingObject).OfType<IIfcProduct>());
            roots.AddRange(spatialAggregations);
            roots.AddRange(spatialRels);
            roots.AddRange(spatialRefs);

            //we should add any feature elements used to subtract mass from a product
            var featureRels = GetFeatureRelations(_primaryElements).ToList();
            var openings = featureRels.Select(r => r.RelatedOpeningElement);
            _primaryElements.AddRange(openings);
            roots.AddRange(featureRels);

            //object types and properties for all primary products (elements and spatial elements)
            roots.AddRange(model.Instances.Where<IIfcRelDefinesByProperties>(r => _primaryElements.Any(e => r.RelatedObjects.Contains(e))));
            roots.AddRange(model.Instances.Where<IIfcRelDefinesByType>(r => _primaryElements.Any(e => r.RelatedObjects.Contains(e))));



            //assignmnet to groups will bring in all system aggregarions if defined in the file
            roots.AddRange(model.Instances.Where<IIfcRelAssigns>(r => _primaryElements.Any(e => r.RelatedObjects.Contains(e))));

            //associations with classification, material and documents
            roots.AddRange(model.Instances.Where<IIfcRelAssociates>(r => _primaryElements.Any(e => r.RelatedObjects.Contains(e))));

            return roots;
        }

        private int GetLabel(string value)
        {
            if (value.StartsWith("#"))
            {
                var trim = value.Trim('#');
                int i;
                if (int.TryParse(trim, out i))
                    return i;
                return -1;
            }

            int j;
            if (int.TryParse(value, out j))
                return j;
            return -1;
        }

        private IEnumerable<IIfcRelVoidsElement> GetFeatureRelations(IEnumerable<IIfcProduct> products)
        {
            var elements = products.OfType<IIfcElement>().ToList();
            if (!elements.Any()) yield break;
            var model = elements.First().Model;
            var rels = model.Instances.Where<IIfcRelVoidsElement>(r => elements.Any(e => Equals(e, r.RelatingBuildingElement)));
            foreach (var rel in rels)
                yield return rel;
        }

        private List<IIfcProduct> _decomposition = new List<IIfcProduct>();

        private IEnumerable<IIfcRelDecomposes> GetAggregations(List<IIfcProduct> products, IModel model)
        {
            _decomposition = new List<IIfcProduct>();
            while (true)
            {
                if (!products.Any())
                    yield break;

                var products1 = products;
                var rels = model.Instances.Where<IIfcRelDecomposes>(r =>
                {
                    var aggr = r as IIfcRelAggregates;
                    if (aggr != null)
                        return products1.Any(p => Equals(aggr.RelatingObject, p));
                    var nest = r as IIfcRelNests;
                    if (nest != null)
                        return products1.Any(p => Equals(nest.RelatingObject, p));
                    var prj = r as IIfcRelProjectsElement;
                    if (prj != null)
                        return products1.Any(p => Equals(prj.RelatingElement, p));
                    var voids = r as IIfcRelVoidsElement;
                    if (voids != null)
                        return products1.Any(p => Equals(voids.RelatingBuildingElement, p));
                    return false;

                }).ToList();
                var relatedProducts = rels.SelectMany(r =>
                {
                    var aggr = r as IIfcRelAggregates;
                    if (aggr != null)
                        return aggr.RelatedObjects.OfType<IIfcProduct>();
                    var nest = r as IIfcRelNests;
                    if (nest != null)
                        return nest.RelatedObjects.OfType<IIfcProduct>();
                    var prj = r as IIfcRelProjectsElement;
                    if (prj != null)
                        return new IIfcProduct[] { prj.RelatedFeatureElement };
                    var voids = r as IIfcRelVoidsElement;
                    if (voids != null)
                        return new IIfcProduct[] { voids.RelatedOpeningElement };
                    return null;
                }).Where(p => p != null).ToList();

                foreach (var rel in rels)
                    yield return rel;

                products = relatedProducts;
                _decomposition.AddRange(products);
            }
        }

        private IEnumerable<IIfcRelAggregates> GetUpstreamHierarchy(IEnumerable<IIfcSpatialElement> spatialStructureElements, IModel model)
        {
            while (true)
            {
                var elements = spatialStructureElements.ToList();
                if (!elements.Any())
                    yield break;

                var rels = model.Instances.Where<IIfcRelAggregates>(r => elements.Any(s => r.RelatedObjects.Contains(s))).ToList();
                var decomposing = rels.Select(r => r.RelatingObject).OfType<IIfcSpatialStructureElement>();

                foreach (var rel in rels)
                    yield return rel;

                spatialStructureElements = decomposing;
            }
        }

        private List<IIfcProduct> _primaryElements;

        private object Filter(ExpressMetaProperty property, object parentObject)
        {
            if (_primaryElements != null && _primaryElements.Any())
            {
                if (typeof(IIfcProduct).IsAssignableFrom(property.PropertyInfo.PropertyType))
                {
                    var element = property.PropertyInfo.GetValue(parentObject, null) as IIfcProduct;
                    if (element != null && _primaryElements.Contains(element))
                        return element;
                    return null;
                }
                if (typeof(IEnumerable<IPersistEntity>).IsAssignableFrom(property.PropertyInfo.PropertyType))
                {
                    var entities = ((IEnumerable<IPersistEntity>)property.PropertyInfo.GetValue(parentObject, null)).ToList();
                    if (!entities.Any())
                        return null;
                    var elementsToRemove = entities.OfType<IIfcProduct>().Where(e => !_primaryElements.Contains(e)).ToList();
                    //if there are no IfcElements return what is in there with no care
                    if (elementsToRemove.Any())
                        //return original values excluding elements not included in the primary set
                        return entities.Except(elementsToRemove).ToList();
                }
            }



            //if geometry is to be included don't filter it out
            if (_includeGeometry)
                return property.PropertyInfo.GetValue(parentObject, null);

            //leave out geometry and placement of products
            if (parentObject is IIfcProduct &&
                (property.PropertyInfo.Name == "Representation" || property.PropertyInfo.Name == "ObjectPlacement")
                )
                return null;

            //leave out representation maps
            if (parentObject is IIfcTypeProduct && property.PropertyInfo.Name == "RepresentationMaps")
                return null;

            //leave out eventual connection geometry
            if (parentObject is IIfcRelSpaceBoundary && property.PropertyInfo.Name == "ConnectionGeometry")
                return null;

            //return the value for anything else
            return property.PropertyInfo.GetValue(parentObject, null);
        }
    }
}
