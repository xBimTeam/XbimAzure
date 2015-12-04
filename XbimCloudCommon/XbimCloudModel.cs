using System;

namespace XbimCloudCommon
{
    public class XbimCloudModel
    {
        public XbimCloudModel()
        {
            //initial value created from Guid which is always unique
            ModelId = Guid.NewGuid().ToString();
            CreateGeometry = true;
        }
        public string ModelId { get;  set; }

        public string Extension { get; set; }
        public string Extension2 { get; set; }

        public bool CreateGeometry { get; set; }

        public string Ids { get; set; }

    }
}
