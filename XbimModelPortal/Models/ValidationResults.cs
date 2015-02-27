using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Web;

namespace XbimModelPortal.Models
{
    public class ValidationResults: List<ResultSet>
    {
    }

    public class ResultSet
    {
        public string ClassificationCode { get; set; }
        public  List<ElementResult> ElementResults { get; set; }
    }

    public class ElementResult
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Passed { get; set; }
        public List<ValidationError> Errors { get; set; }
    }

    public class ValidationError
    {
        public string Parameter { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
    }
}