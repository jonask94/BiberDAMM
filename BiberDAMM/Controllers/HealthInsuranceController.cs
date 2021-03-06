﻿using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using BiberDAMM.DAL;
using BiberDAMM.Models;
using BiberDAMM.Security;
using BiberDAMM.Helpers;

namespace BiberDAMM.Controllers
{
    // ===============================
    // AUTHOR     : ChristesR
    // ===============================
    [CustomAuthorize]
    public class HealthInsuranceController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        //Generating Index Page
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator+","+ ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult Index()
        {
            var healthInsurances = from m in db.HealthInsurances
                                   select m;
            return View(healthInsurances.OrderBy(o => o.Name));
        }

        //Generating Details Page
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult Details(int? id)
        {
            if (id == null)
                return RedirectToAction("Index");
            var healthInsurance = db.HealthInsurances.Find(id);
            if (healthInsurance == null)
                return HttpNotFound();
            return View(healthInsurance);
        }


        //Getter and Setter for Creation-Page
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult Create(HealthInsurance healthInsurance)
        {
            if (ModelState.IsValid)
            {
                //Check if HealthInsurance with number already exists
                var healthInsurances = from m in db.HealthInsurances
                                       select m;
                healthInsurances = healthInsurances.Where(h => h.Number.Equals(healthInsurance.Number));

                if (healthInsurances.Count() != 0)
                {
                    TempData["HealthInsuranceError"] = "Versicherungskennung bereits vergeben";
                    return View(healthInsurance);
                }
                db.HealthInsurances.Add(healthInsurance);
                db.SaveChanges();
                TempData["HealthInsuranceSuccess"] = "Daten erfolgreich gespeichert";
                return RedirectToAction("Index");
            }

            TempData["HealthInsuranceError"] = "Eingaben fehlerhaft oder unvollständig";
            return View(healthInsurance);
        }


        //Getter and Setter for Edit-Page
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return RedirectToAction("Index");
            var healthInsurance = db.HealthInsurances.Find(id);
            if (healthInsurance == null)
                return HttpNotFound();
            return View(healthInsurance);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult Edit(HealthInsurance healthInsurance)
        {
            if (ModelState.IsValid)
            {

                //Check if HealthInsurance with number already exists
                var healthInsurances = from m in db.HealthInsurances
                                       select m;
                healthInsurances = healthInsurances.Where(h => h.Number.Equals(healthInsurance.Number)&& !h.Name.Equals(healthInsurance.Name));

                if (healthInsurances.Count() != 0)
                {
                    TempData["HealthInsuranceError"] = "Versicherungskennung bereits vergeben";
                    return View(healthInsurance);
                }
                db.Entry(healthInsurance).State = EntityState.Modified;
                db.SaveChanges();

                TempData["HealthInsuranceSuccess"] = "Daten erfolgreich gespeichert";
                return RedirectToAction("Details", new { id = healthInsurance.Id });
            }

            TempData["HealthInsuranceError"] = "Eingaben fehlerhaft oder unvollständig";
            return View(healthInsurance);
        }


        //Function for deleting Datasets
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult DeleteConfirmed(int id)
        {
            var healthInsurance = db.HealthInsurances.Find(id);
            if (healthInsurance.Client.Count != 0)
            {
                TempData["HealthInsuranceError"] = "Der Versicherung sind noch Patienten zugeordnet";
                return RedirectToAction("Details", "HealthInsurance", new { id = healthInsurance.Id });
            }
            db.HealthInsurances.Remove(healthInsurance);
            db.SaveChanges();

            TempData["HealthInsuranceSuccess"] = "Versicherung erfolgreich gelöscht";
            return RedirectToAction("Index");
        }

        //Function for Redirecting HealthInsurance to Client
        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult AddInsuranceToClient(int id)
        {
            HealthInsurance healthInsurance = db.HealthInsurances.Find(id);
            if (healthInsurance == null)
                return HttpNotFound();

            Client temp = new Client();

            //Referencing to a editing Client Method
            if (Session["TempClient"] != null)
            {
                temp = (Client)Session["TempClient"];
            }
            //Referencing to a new Client Method
            else
            {
                temp = (Client)Session["TempNewClient"];
            }


            temp.HealthInsurance = healthInsurance;
            temp.HealthInsuranceId = healthInsurance.Id;


            if (Session["TempClient"] != null)
            {
                Session["TempClient"] = temp;

                return RedirectToAction("Edit", "Client", new { id = temp.Id });
            }
            else
            {
                Session["TempNewClient"] = temp;

                return RedirectToAction("Create", "Client");
            }


        }

        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        public ActionResult CancelEditingOrCreatingClient()
        {
            Session["TempClient"] = null;
            Session["TempNewClient"] = null;
            TempData["HealthInsuranceError"] = "Bearbeitung abgebrochen";
            return RedirectToAction("Index", "HealthInsurance");
        }

        [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}