using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace XbimModelPortal.Controllers
{
    public class ComplianceController : CloudStorageController
    {
        // GET: Compliance
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Convert()
        {
            return View();
        }

        public ActionResult Review()
        {
            return RedirectToAction("Index","Model");
        }

        public ActionResult Validate()
        {
            return View();
        }

        public ActionResult Verify()
        {
            return View();
        }

    }
}