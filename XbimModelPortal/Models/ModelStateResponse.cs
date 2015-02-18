using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace XbimModelPortal.Models
{
    /// <summary>
    /// POCO object which will be serialized as a JSON
    /// </summary>
    public class ModelStateResponse
    {
        public string State { get; set; }

        public string ModelName { get; set; }

        public bool Finished { get; set; }

        public string WexBIMName { get; set; }
        
        public string COBieName { get; set; }

        public string Message { get; set; }
    }
}