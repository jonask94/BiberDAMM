﻿using BiberDAMM.DAL;
using System.Web.Mvc;
using System.Linq;
using System.Collections.Generic;
using BiberDAMM.ViewModels;
using BiberDAMM.Helpers;
using BiberDAMM.Models;
using System.Collections.ObjectModel;
using System;
using System.Data.Entity.Migrations;
using BiberDAMM.Security;

namespace BiberDAMM.Controllers
{
    [CustomAuthorize(Roles = ConstVariables.RoleAdministrator + "," + ConstVariables.RoleDoctor + "," + ConstVariables.RoleNurse + "," + ConstVariables.RoleTherapist)]
    public class TreatmentController : Controller
    {
        //The Database-Context [KrabsJ]
        private ApplicationDbContext _db = new ApplicationDbContext();

        // GET Treatment/SelectTreatmentType/id [KrabsJ]
        // first step of creating a new treatment is to select a treatment type
        // this method returns the view for this step
        // expected parameter: id of a stay
        // return: view("SelectTreatmentType", CreationTreatmentSelectType viewModel)
        public ActionResult SelectTreatmentType(int id)
        {
            // check if the stay with the given id exists
            int stayId;
            if (_db.Stays.Any(s => s.Id == id))
            {
                stayId = id;
            }
            else
            {
                // if there is no stay with the given id, show home index page and failure alert
                TempData["UnexpectedFailure"] = " Es konnte kein Aufenthalt mit der übergebenen Id gefunden werden.";
                return RedirectToAction("Index", "Home");
            }

            //get all treatment types from db and put them into a selectList
            var listTreatmentTypes = _db.TreatmentTypes.ToList();
            var selectionListTreatmentTypes = new List<SelectListItem>();
            foreach (var t in listTreatmentTypes)
            {
                selectionListTreatmentTypes.Add(new SelectListItem { Text = (t.Name), Value = (t.Id.ToString()) });
            }

            // create the viewModel for the view
            var treatmentCreationSelectTypeModel = new CreationTreatmentSelectType();
            treatmentCreationSelectTypeModel.StayId = stayId;
            treatmentCreationSelectTypeModel.ListTreatmentTypes = selectionListTreatmentTypes;
            treatmentCreationSelectTypeModel.ClientId = _db.Stays.Where(s => s.Id == stayId).FirstOrDefault().ClientId;
            Client client = _db.Clients.Where(c => c.Id == treatmentCreationSelectTypeModel.ClientId).FirstOrDefault();
            treatmentCreationSelectTypeModel.ClientName = client.Surname + " " + client.Lastname;

            // return view
            return View(treatmentCreationSelectTypeModel);
        }

        //POST: Treatment/SelectTreatmentType [KrabsJ]
        // this method transfers to the create method after selecting a treatment type
        // expected parameter: CreationTreatmentSelectType viewModel1, string command)
        // return: view("Create", CreationTreatmentSelectType viewModel1)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SelectTreatmentType(CreationTreatmentSelectType treatmentCreationSelectTypeModel, string command)
        {
            // if the abort button was clicked return to the stay details page
            if (command.Equals(ConstVariables.AbortButton))
                return RedirectToAction("Details", "Stay", new { id = treatmentCreationSelectTypeModel.StayId });

            // else go to the create method
            return RedirectToAction("Create", "Treatment", treatmentCreationSelectTypeModel);

        }

        // GET: Treatment/Create [KrabsJ]
        // this method return the view "create" that enables the user to create a new treatment
        // the method loads the data that is necessary for creation
        // expected parameter: CreationTreatmentSelectType viewModel1
        // return: view("create", CreationTreatment viewModel2)
        public ActionResult Create(CreationTreatmentSelectType treatmentCreationSelectTypeModel)
        {

            // load data from the CreationTreatmentSelectType-viewmodel to a CreationTreatment-viewmodel
            CreationTreatment treatmentCreationModel = new CreationTreatment();
            treatmentCreationModel.Id = null;
            treatmentCreationModel.StayId = treatmentCreationSelectTypeModel.StayId;
            treatmentCreationModel.TreatmentTypeId = treatmentCreationSelectTypeModel.TreatmentTypeId;
            treatmentCreationModel.ClientId = treatmentCreationSelectTypeModel.ClientId;
            treatmentCreationModel.ClientName = treatmentCreationSelectTypeModel.ClientName;
            treatmentCreationModel.IsStoredWithSeries = false;
            treatmentCreationModel.IdOfSeries = null;
            treatmentCreationModel.CleaningId = null;

            Blocks currentBlock = _db.Blocks.Where(b => b.BeginDate <= DateTime.Now && b.EndDate >= DateTime.Now && b.StayId == treatmentCreationModel.StayId).FirstOrDefault();
            if (currentBlock != null)
            {
                treatmentCreationModel.ClientRoomNumber = currentBlock.Bed.Room.RoomNumber;
            }

            // set defaultDate for calendar in view
            treatmentCreationModel.ShowCalendarDay = DateTime.Now.ToString("s");

            //load selectedTreatmentType from db and set attribute TreatmentTypeName of viewModel
            TreatmentType selectedTreatmentType = _db.TreatmentTypes.Where(t => t.Id == treatmentCreationModel.TreatmentTypeId).FirstOrDefault();
            treatmentCreationModel.TreatmentTypeName = selectedTreatmentType.Name;

            //load the rooms that are available for the selectedTreatmentType
            ICollection<Room> rooms = new Collection<Room>();
            if (selectedTreatmentType.RoomTypeId == null)
            {
                rooms = _db.Rooms.ToList();
            }
            else
            {
                rooms = _db.Rooms.Where(r => r.RoomTypeId == selectedTreatmentType.RoomTypeId).ToList();
            }

            //convert the list of rooms to a list of SelectionRooms (this class only contains the attributes that are necessary for creating a new treatment)
            treatmentCreationModel.Rooms = new List<SelectionRoom>();
            foreach (var item in rooms)
            {
                SelectionRoom selectionRoom = new SelectionRoom();
                selectionRoom.Id = item.Id;
                selectionRoom.RoomNumber = item.RoomNumber;
                selectionRoom.RoomTypeName = item.RoomType.Name;
                treatmentCreationModel.Rooms.Add(selectionRoom);
            }

            // get all users (besides cleaners & adminstrators) from the db
            ICollection<ApplicationUser> userList = new Collection<ApplicationUser>();
            userList = _db.Users.Where(u => u.UserType.ToString() != ConstVariables.RoleCleaner && u.UserType.ToString() != ConstVariables.RoleAdministrator).ToList();

            // convert the list of users into a list of staffmembers
            treatmentCreationModel.Staff = new List<Staff>();
            foreach (var item in userList)
            {
                Staff staffMember = new Staff();
                staffMember.Id = item.Id;
                if (item.Title == null)
                {
                    staffMember.DisplayName = item.Surname + " " + item.Lastname;
                }
                else
                {
                    staffMember.DisplayName = item.Title + " "  + item.Surname + " " + item.Lastname;
                }
                staffMember.Selected = false;
                staffMember.StaffType = item.UserType;
                treatmentCreationModel.Staff.Add(staffMember);
            }

            // get all client appointments and store them in the viewModel
            treatmentCreationModel.AppointmentsOfSelectedRessources = GetClientAppointments(treatmentCreationModel.ClientId, null);

            // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
            if (treatmentCreationModel.AppointmentsOfSelectedRessources == null)
            {
                treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
            }
            treatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentCreationModel.AppointmentsOfSelectedRessources);

            //return view
            return View(treatmentCreationModel);
        }

        // POST: Treatment/Create [KrabsJ]
        // This method activates the update of the viewModel if a new room or new staff was selected and it stores new treatments in the db
        // expected parameter: CreationTreatment viewModel, string command
        // return: View(CreationTreatment viewModel) or RedirectToAction("Details", "Stay", new { id = treatmentCreationModel.StayId })
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreationTreatment treatmentCreationModel, string command)
        {
            // if abortButton was clicked, go back to details page of stay or of treatment
            if (command.Equals(ConstVariables.AbortButton))
            {
                if (treatmentCreationModel.Id != null)
                {
                    return RedirectToAction("Details", "Treatment", new { Id = treatmentCreationModel.Id });
                }
                return RedirectToAction("Details", "Stay", new { id = treatmentCreationModel.StayId });
            }

            // set defaultDate for calendar in view
            treatmentCreationModel.ShowCalendarDay = treatmentCreationModel.BeginDate.GetValueOrDefault(DateTime.Now).Date.ToString("s");

            // if button "Aktualisieren" was clicked, update the appointments of selected ressources and update the planned treatment to show it in the calendar
            if (command.Equals(ConstVariables.Update))
            {
                // update all data in the viewModel
                CreationTreatment updatedTreatmentCreationModel = UpdateViewModelByPlannedTreatment(treatmentCreationModel);
                updatedTreatmentCreationModel = UpdateViewModelClientAppointments(updatedTreatmentCreationModel);
                updatedTreatmentCreationModel = UpdateViewModelByRoomSelection(updatedTreatmentCreationModel);
                updatedTreatmentCreationModel = UpdateViewModelByStaffSelection(updatedTreatmentCreationModel);

                // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                if (updatedTreatmentCreationModel.AppointmentsOfSelectedRessources == null)
                {
                    updatedTreatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                }
                updatedTreatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(updatedTreatmentCreationModel.AppointmentsOfSelectedRessources);

                //clear ModeState, so that values are loaded from the updated model
                ModelState.Clear();

                return View(updatedTreatmentCreationModel);
            }

            //if a new room was selected, update the viewModel and return the View again
            if (command.Equals(ConstVariables.UseRoom))
            {
                CreationTreatment updatedTreatmentCreationModel = UpdateViewModelByRoomSelection(treatmentCreationModel);
                updatedTreatmentCreationModel = UpdateViewModelByPlannedTreatment(updatedTreatmentCreationModel);

                // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                if (updatedTreatmentCreationModel.AppointmentsOfSelectedRessources == null)
                {
                    updatedTreatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                }
                updatedTreatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(updatedTreatmentCreationModel.AppointmentsOfSelectedRessources);

                //clear ModeState, so that values are loaded from the updated model
                ModelState.Clear();

                return View(updatedTreatmentCreationModel);
            }

            //if new staff was selected, update the viewModel and return the View again
            if (command.Equals(ConstVariables.UseStaff))
            {
                CreationTreatment updatedTreatmentCreationModel = UpdateViewModelByStaffSelection(treatmentCreationModel);
                updatedTreatmentCreationModel = UpdateViewModelByPlannedTreatment(updatedTreatmentCreationModel);

                // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                if (updatedTreatmentCreationModel.AppointmentsOfSelectedRessources == null)
                {
                    updatedTreatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                }
                updatedTreatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(updatedTreatmentCreationModel.AppointmentsOfSelectedRessources);

                //clear ModeState, so that values are loaded from the updated model
                ModelState.Clear();

                return View(updatedTreatmentCreationModel);
            }

            // if the user want to save the new treatment check if the model is valid
            if (ModelState.IsValid)
            {
                // for the following checks it is necessary that the data is up-to-date:
                treatmentCreationModel = UpdateViewModelByPlannedTreatment(treatmentCreationModel);
                treatmentCreationModel = UpdateViewModelClientAppointments(treatmentCreationModel);
                treatmentCreationModel = UpdateViewModelByRoomSelection(treatmentCreationModel);
                treatmentCreationModel = UpdateViewModelByStaffSelection(treatmentCreationModel);

                // extract the appointments that represent the planned treatments and cleaning events
                // these time periods have to be checked on conflicts
                List<AppointmentOfSelectedRessource> checkListOfPlannedTreatments = treatmentCreationModel.AppointmentsOfSelectedRessources.Where(a => a.Ressource == ConstVariables.PlannedTreatment).ToList();
                List<AppointmentOfSelectedRessource> checkListOfPlannedCleanings = treatmentCreationModel.AppointmentsOfSelectedRessources.Where(a => a.Ressource == ConstVariables.PlannedCleaning).ToList();

                // extract the appointments that represent appointments of the room, the client and the staffmembers
                // these time periods work as control instances
                List<AppointmentOfSelectedRessource> controlListOfFutureClientAppointments = treatmentCreationModel.AppointmentsOfSelectedRessources.Where(a => a.Ressource == ConstVariables.AppointmentOfClient).ToList();
                List<AppointmentOfSelectedRessource> controlListOfFutureRoomAppointments = treatmentCreationModel.AppointmentsOfSelectedRessources.Where(a => a.Ressource == ConstVariables.AppointmentOfRoom).ToList();
                List<AppointmentOfSelectedRessource> controlListOfFutureStaffAppointments = treatmentCreationModel.AppointmentsOfSelectedRessources.Where(a => a.Ressource != ConstVariables.AppointmentOfClient && a.Ressource != ConstVariables.AppointmentOfRoom && a.Ressource != ConstVariables.PlannedCleaning && a.Ressource != ConstVariables.PlannedTreatment).ToList();

                // ensure that BeginDate is before EndDate
                if (treatmentCreationModel.BeginDate >= treatmentCreationModel.EndDate)
                {
                    // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                    if (treatmentCreationModel.AppointmentsOfSelectedRessources == null)
                    {
                        treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                    }
                    treatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentCreationModel.AppointmentsOfSelectedRessources);

                    // error-message for alert-statement [KrabsJ]
                    TempData["BeginDateEndDateError"] = " Der Behandlungsbeginn muss vor dem Behandlungsende liegen.";

                    // return view
                    return View(treatmentCreationModel);
                }

                // check series and seriescounter
                if (treatmentCreationModel.Series != Series.noSeries && treatmentCreationModel.SeriesCounter == 0)
                {
                    // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                    if (treatmentCreationModel.AppointmentsOfSelectedRessources == null)
                    {
                        treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                    }
                    treatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentCreationModel.AppointmentsOfSelectedRessources);

                    // error-message for alert-statement [KrabsJ]
                    TempData["InvalidSeriesValues"] = " Sie haben angegeben, dass Sie einen Serientermin planen möchten. Bitte geben Sie eine Anzahl an Wiederholungen an.";

                    // return view
                    return View(treatmentCreationModel);
                }

                // ensure that the treatments are whithin the timeperiod of the stay
                var stay = _db.Stays.Where(s => s.Id == treatmentCreationModel.StayId).FirstOrDefault();
                foreach (var plannedTreatment in checkListOfPlannedTreatments)
                {
                    if ((stay.EndDate != null && stay.EndDate < plannedTreatment.EndDate) || (stay.BeginDate > plannedTreatment.BeginDate))
                    {
                        // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                        if (treatmentCreationModel.AppointmentsOfSelectedRessources == null)
                        {
                            treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                        }
                        treatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentCreationModel.AppointmentsOfSelectedRessources);

                        // error-message for alert-statement [KrabsJ]
                        if (stay.EndDate != null)
                        {
                            TempData["StayClosedError"] = " Der zugehörige Aufenthalt des Patienten beginnt am " + stay.BeginDate + " und endet am " + stay.EndDate + ". Mindestens ein geplanter Behandlungstermin befindet sich nicht im Zeitraum des Aufenthalts.";
                        }
                        else
                        {
                            TempData["StayClosedError"] = " Der zugehörige Aufenthalt des Patienten beginnt am " + stay.BeginDate + ". Mindestens ein geplanter Behandlungstermin befindet sich nicht im Zeitraum des Aufenthalts.";
                        }

                        // return view
                        return View(treatmentCreationModel);
                    }
                }

                // if the new treatment is planed in the future it is necessary to check if there are conflicting appointments
                // therefor it is necessary to determine the end of the treatment while considering an optional cleaning event
                DateTime endOfTreatmentAndCleaning = treatmentCreationModel.EndDate.Value;
                switch (treatmentCreationModel.CleaningDuration)
                {
                    case CleaningDuration.noCleaning:
                        break;
                    case CleaningDuration.tenMinutes:
                        endOfTreatmentAndCleaning = treatmentCreationModel.EndDate.Value.AddMinutes(10);
                        break;
                    case CleaningDuration.twentyMinutes:
                        endOfTreatmentAndCleaning = treatmentCreationModel.EndDate.Value.AddMinutes(20);
                        break;
                    case CleaningDuration.thirtyMinutes:
                        endOfTreatmentAndCleaning = treatmentCreationModel.EndDate.Value.AddMinutes(30);
                        break;
                    default:
                        break;
                }                

                //helper list for unordered conflicts
                List<AppointmentOfSelectedRessource> unorderedConflicts = new List<AppointmentOfSelectedRessource>();

                //initialize list of conflicting appointments
                treatmentCreationModel.ConflictingAppointmentsList = new List<AppointmentOfSelectedRessource>();

                // the following steps ensure that there are no conflicts with other appointments
                // if the planned treatment is in the future: check on conflicts
                if (endOfTreatmentAndCleaning > DateTime.Now)
                {
                    // check all planned treatments for conflicts with other appointments
                    foreach (var plannedTreatment in checkListOfPlannedTreatments)
                    {
                        // check if there are conflicts with other appointments of the client
                        var conflictingClientAppointments = controlListOfFutureClientAppointments.Where(a => a.BeginDate < plannedTreatment.EndDate && plannedTreatment.BeginDate < a.EndDate).ToList();
                        if (conflictingClientAppointments.Count > 0)
                        {
                            foreach (var appointment in conflictingClientAppointments)
                            {
                                AppointmentOfSelectedRessource newConflict = new AppointmentOfSelectedRessource();
                                newConflict.BeginDate = appointment.BeginDate;
                                newConflict.EndDate = appointment.EndDate;
                                newConflict.Ressource = "Patient";
                                unorderedConflicts.Add(newConflict);
                            }
                        }

                        // check if there are conflicts with other appointments of the selected room (this includes cleaning appointments of this room)
                        var conflictingRoomAppointments = controlListOfFutureRoomAppointments.Where(a => a.BeginDate < plannedTreatment.EndDate && plannedTreatment.BeginDate < a.EndDate).ToList();
                        if (conflictingRoomAppointments.Count > 0)
                        {
                            foreach (var appointment in conflictingRoomAppointments)
                            {
                                AppointmentOfSelectedRessource newConflict = new AppointmentOfSelectedRessource();
                                newConflict.BeginDate = appointment.BeginDate;
                                newConflict.EndDate = appointment.EndDate;
                                newConflict.Ressource = "Raum: " + treatmentCreationModel.SelectedRoomNumber;
                                unorderedConflicts.Add(newConflict);
                            }
                        }

                        // check if there are conflicts with other appointments of the selected staffmembers
                        var conflictingStaffAppointments = controlListOfFutureStaffAppointments.Where(a => a.BeginDate < plannedTreatment.EndDate && plannedTreatment.BeginDate < a.EndDate).ToList();
                        if (conflictingStaffAppointments.Count > 0)
                        {
                            foreach (var appointment in conflictingStaffAppointments)
                            {
                                AppointmentOfSelectedRessource newConflict = new AppointmentOfSelectedRessource();
                                newConflict.BeginDate = appointment.BeginDate;
                                newConflict.EndDate = appointment.EndDate;
                                newConflict.Ressource = appointment.Ressource;
                                unorderedConflicts.Add(newConflict);
                            }
                        }
                    }

                    // check all planned cleanings for conflicts with other appointments (cleanings only have to be checked against room appointments, because other ressources are not effected by cleanings)
                    foreach (var plannedCleaning in checkListOfPlannedCleanings)
                    {
                        // check if there are conflicts with other appointments of the selected room (this includes cleaning appointments of this room)
                        var conflictingRoomAppointments = controlListOfFutureRoomAppointments.Where(a => a.BeginDate < plannedCleaning.EndDate && plannedCleaning.BeginDate < a.EndDate).ToList();
                        if (conflictingRoomAppointments.Count > 0)
                        {
                            foreach (var appointment in conflictingRoomAppointments)
                            {
                                AppointmentOfSelectedRessource newConflict = new AppointmentOfSelectedRessource();
                                newConflict.BeginDate = appointment.BeginDate;
                                newConflict.EndDate = appointment.EndDate;
                                newConflict.Ressource = "Raum: " + treatmentCreationModel.SelectedRoomNumber;
                                unorderedConflicts.Add(newConflict);
                            }
                        }
                    }

                    // store an ordered list of the conflicts in the viewModel
                    treatmentCreationModel.ConflictingAppointmentsList = unorderedConflicts.OrderBy(c => c.BeginDate).ToList();

                    // if there are any conflicts the treatment is not stored in the db
                    if (treatmentCreationModel.ConflictingAppointmentsList.Count > 0)
                    {
                        // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                        if (treatmentCreationModel.AppointmentsOfSelectedRessources == null)
                        {
                            treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                        }
                        treatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentCreationModel.AppointmentsOfSelectedRessources);

                        // error-message for alert-statement [KrabsJ]
                        TempData["ConflictingAppointments"] = " Es wurden Konflikte mit anderen Terminen gefunden. ";

                        // return view
                        return View(treatmentCreationModel);
                    }
                }

                // this point is reached if there are no conflicting appointments or if the treatment is stored retroactive
                // a treatment that is stored retroactive (in the past) doesn't have to be checked on conflicts

                // ensure that a treatment that is stored retroactive does not have series events
                if (endOfTreatmentAndCleaning <= DateTime.Now && treatmentCreationModel.Series != Series.noSeries)
                {
                        // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
                        if (treatmentCreationModel.AppointmentsOfSelectedRessources == null)
                        {
                            treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                        }
                        treatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentCreationModel.AppointmentsOfSelectedRessources);

                        // error-message for alert-statement [KrabsJ]
                        TempData["SeriesInPastError"] = " Aus der Vergangenheit heraus können keine Serienbehandlungen geplant werden.";

                        // return view
                        return View(treatmentCreationModel);
                }

                List<ApplicationUser> userList = new List<ApplicationUser>();
                if (treatmentCreationModel.SelectedStaff != null)
                {
                    foreach (var staffMember in treatmentCreationModel.SelectedStaff)
                    {
                        var user = _db.Users.Single(u => u.Id == staffMember.Id);
                        userList.Add(user);
                    }
                }

                // helper variables for storing information about series
                int idOfSeries = 0;
                int loopCounter = 1;

                // create the new treatments and store or update them in the db
                foreach (var plannedTreatment in checkListOfPlannedTreatments)
                {
                    var newTreatment = new Treatment
                    {
                        BeginDate = plannedTreatment.BeginDate,
                        EndDate = plannedTreatment.EndDate,
                        StayId = treatmentCreationModel.StayId,
                        RoomId = treatmentCreationModel.SelectedRoomId,
                        Description = treatmentCreationModel.Description,
                        TreatmentTypeId = treatmentCreationModel.TreatmentTypeId,
                        UpdateTimeStamp = DateTime.Now,
                        ApplicationUsers = userList,
                    };
                    // if loopCounter is greater than 1, it is a series of treatments that is stored in th db
                    // in this case, the idOfSeries is the ID of the first treatment that is stored
                    if (loopCounter > 1)
                    {
                        newTreatment.IdOfSeries = idOfSeries;
                    }

                    // if this method is called for editing an already stored treatment, this treatment has got an ID
                    if (plannedTreatment.IsOriginalAppointment == true && treatmentCreationModel.Id != null)
                    {
                        newTreatment.Id = treatmentCreationModel.Id.Value;
                        // the method "addOrUpdate" doesn't update many-to-many tables --> this has to be made manually
                        Treatment editTreatment = _db.Treatments.Single(t => t.Id == newTreatment.Id);
                        editTreatment.ApplicationUsers.Clear();
                        foreach (var user in userList)
                        {
                            editTreatment.ApplicationUsers.Add(user);
                        }
                        _db.SaveChanges();
                    }
                    _db.Treatments.AddOrUpdate(newTreatment);
                    _db.SaveChanges();

                    // if a series of treatment should be stored, remember the ID of the first stored treatment, because this is used as the idOfSeries
                    // also this attribute has to be set to the first stored treatment
                    if (loopCounter == 1 && checkListOfPlannedTreatments.Count > 1)
                    {
                        idOfSeries = newTreatment.Id;
                        newTreatment.IdOfSeries = idOfSeries;
                        _db.Treatments.AddOrUpdate(newTreatment);
                        _db.SaveChanges();
                    }
                    else if (treatmentCreationModel.IdOfSeries != null)
                    {
                        // a treatment that is edited could already have an idOfSeries
                        idOfSeries = treatmentCreationModel.IdOfSeries.Value;
                        newTreatment.IdOfSeries = idOfSeries;
                        _db.Treatments.AddOrUpdate(newTreatment);
                        _db.SaveChanges();
                    }

                    // create an optional cleaningAppointment
                    switch (treatmentCreationModel.CleaningDuration)
                    {
                        case CleaningDuration.noCleaning:
                            // if a treatment is edited, check if there is an associated cleaning event, that should be removed, 
                            if (plannedTreatment.IsOriginalAppointment ==  true && treatmentCreationModel.CleaningId != null)
                            {
                                Cleaner deleteCleaner = _db.Cleaner.Single(c => c.Id == treatmentCreationModel.CleaningId);
                                _db.Cleaner.Remove(deleteCleaner);
                                _db.SaveChanges();
                            }
                            break;
                        case CleaningDuration.tenMinutes:
                            var newCleaningAppointmentTen = new Cleaner
                            {
                                BeginDate = plannedTreatment.EndDate,
                                EndDate = plannedTreatment.EndDate.AddMinutes(10),
                                RoomId = treatmentCreationModel.SelectedRoomId,
                                CleaningDone = false,
                                CleaningDuration = CleaningDuration.tenMinutes,
                                TreatmentId = newTreatment.Id
                            };
                            if (plannedTreatment.IsOriginalAppointment == true && treatmentCreationModel.CleaningId != null)
                            {
                                newCleaningAppointmentTen.Id = treatmentCreationModel.CleaningId.Value;
                            }
                            _db.Cleaner.AddOrUpdate(newCleaningAppointmentTen);
                            _db.SaveChanges();
                            break;
                        case CleaningDuration.twentyMinutes:
                            var newCleaningAppointmentTwenty = new Cleaner
                            {
                                BeginDate = plannedTreatment.EndDate,
                                EndDate = plannedTreatment.EndDate.AddMinutes(20),
                                RoomId = treatmentCreationModel.SelectedRoomId,
                                CleaningDone = false,
                                CleaningDuration = CleaningDuration.twentyMinutes,
                                TreatmentId = newTreatment.Id
                            };
                            if (plannedTreatment.IsOriginalAppointment == true && treatmentCreationModel.CleaningId != null)
                            {
                                newCleaningAppointmentTwenty.Id = treatmentCreationModel.CleaningId.Value;
                            }
                            _db.Cleaner.AddOrUpdate(newCleaningAppointmentTwenty);
                            _db.SaveChanges();
                            break;
                        case CleaningDuration.thirtyMinutes:
                            var newCleaningAppointmentThirty = new Cleaner
                            {
                                BeginDate = plannedTreatment.EndDate,
                                EndDate = plannedTreatment.EndDate.AddMinutes(30),
                                RoomId = treatmentCreationModel.SelectedRoomId,
                                CleaningDone = false,
                                CleaningDuration = CleaningDuration.thirtyMinutes,
                                TreatmentId = newTreatment.Id
                            };
                            if (plannedTreatment.IsOriginalAppointment == true && treatmentCreationModel.CleaningId != null)
                            {
                                newCleaningAppointmentThirty.Id = treatmentCreationModel.CleaningId.Value;
                            }
                            _db.Cleaner.AddOrUpdate(newCleaningAppointmentThirty);
                            _db.SaveChanges();
                            break;
                        default:
                            break;
                    }
                    loopCounter = loopCounter + 1;
                }

                if (treatmentCreationModel.Id != null)
                {
                    // success-message for alert-statement
                    TempData["EditTreatmentSuccess"] = " Die Behandlung wurde geändert.";
                    return RedirectToAction("Details", "Treatment", new { Id = treatmentCreationModel.Id });
                }
                else
                {
                    // success-message for alert-statement
                    TempData["NewTreatmentSuccess"] = " Die neue Behandlung wurde gespeichert.";
                    // go back to stays details page
                    return RedirectToAction("Details", "Stay", new { id = treatmentCreationModel.StayId });
                }
            }

            // this point is only reached if the modal was not valid when trying to save the new treatment
            // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
            if (treatmentCreationModel.AppointmentsOfSelectedRessources == null)
            {
                treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
            }
            treatmentCreationModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentCreationModel.AppointmentsOfSelectedRessources);

            // return view
            return View(treatmentCreationModel);
        }

        //[KrabsJ]
        //this method updates the viewModel data depending on the selected room
        //expected parameter: CreationTreatment viewModel
        //return: CreationTreatment viewModel
        private CreationTreatment UpdateViewModelByRoomSelection(CreationTreatment treatmentCreationModel)
        {
            // if there was no room selected the selectedRoomId is 0 [db-IDs start with 1]
            // in this case no data has to be updated
            if (treatmentCreationModel.SelectedRoomId != 0)
            {
                // load the selectedRoomNumber from db and set the ViewModel-attribute
                string selectedRoomNumber = _db.Rooms.Where(r => r.Id == treatmentCreationModel.SelectedRoomId).FirstOrDefault().RoomNumber;
                treatmentCreationModel.SelectedRoomNumber = selectedRoomNumber;

                // remove the appointments of the room that was selected before
                if (treatmentCreationModel.AppointmentsOfSelectedRessources != null)
                {
                    foreach (var appointment in treatmentCreationModel.AppointmentsOfSelectedRessources.ToArray())
                    {
                        if (appointment.Ressource == ConstVariables.AppointmentOfRoom)
                        {
                            treatmentCreationModel.AppointmentsOfSelectedRessources.Remove(appointment);
                        }
                    }
                }
                else
                {
                    // for adding new appointments (see the steps below) it is necessary that the list is not null
                    treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
                }

                // load the appointments of the selected room from db (don't load appointments where the id fits --> this is important for editing treatments)
                var newRoomAppointments = _db.Treatments.Where(t => t.EndDate > DateTime.Now && t.RoomId == treatmentCreationModel.SelectedRoomId && t.Id != treatmentCreationModel.Id).ToList();

                // convert these appointments (class treatment) into objects of AppointmentOfSelectedRessource and add them to treatmentCreationModel.AppointmentsOfSelectedRessources
                foreach (var appointment in newRoomAppointments)
                {
                    AppointmentOfSelectedRessource appointmentOfSelectedRoom = new AppointmentOfSelectedRessource();
                    appointmentOfSelectedRoom.BeginDate = appointment.BeginDate;
                    appointmentOfSelectedRoom.EndDate = appointment.EndDate;
                    appointmentOfSelectedRoom.Ressource = ConstVariables.AppointmentOfRoom;
                    appointmentOfSelectedRoom.EventColor = "#32CD32";
                    treatmentCreationModel.AppointmentsOfSelectedRessources.Add(appointmentOfSelectedRoom);


                    // check if there is a cleaning appointment that relates to the roomappointment
                    var optionalCleaningAppointment = _db.Cleaner.SingleOrDefault(c => c.TreatmentId == appointment.Id);
                    // if there is a cleaning appointment add it to the viewModel
                    if (optionalCleaningAppointment != null)
                    {
                        AppointmentOfSelectedRessource cleaningAppointment = new AppointmentOfSelectedRessource();
                        cleaningAppointment.BeginDate = optionalCleaningAppointment.BeginDate;
                        cleaningAppointment.EndDate = optionalCleaningAppointment.EndDate;
                        cleaningAppointment.EventColor = "#32CD32";
                        cleaningAppointment.Ressource = ConstVariables.AppointmentOfRoom;
                        treatmentCreationModel.AppointmentsOfSelectedRessources.Add(cleaningAppointment);
                    }
                }
            }
            else
            {
                treatmentCreationModel.SelectedRoomNumber = null;
            }

            // return ViewModel
            return treatmentCreationModel;
        }

        //[KrabsJ]
        //this method updates the viewModel data depending on the selected staffMembers
        //expected parameter: CreationTreatment viewModel
        //return: CreationTreatment viewModel
        private CreationTreatment UpdateViewModelByStaffSelection(CreationTreatment treatmentCreationModel)
        {
            //initialize list of selectedStaff (delete old selection)
            treatmentCreationModel.SelectedStaff = new List<Staff>();

            //helper list for unordered staffmembers
            List<Staff> unorderedStaff = new List<Staff>();

            // write selected staffmembers into the unordered list
            if (treatmentCreationModel.Staff != null)
            {
                foreach (var item in treatmentCreationModel.Staff)
                {
                    if (item.Selected == true)
                    {
                        unorderedStaff.Add(item);
                    }
                }
            }

            // sort list by DisplayName and write it into the list of viewModel
            treatmentCreationModel.SelectedStaff = unorderedStaff.OrderBy(s => s.DisplayName).ToList();

            // remove the appointments of the staffmembers that were selected before
            if (treatmentCreationModel.AppointmentsOfSelectedRessources != null)
            {
                foreach (var appointment in treatmentCreationModel.AppointmentsOfSelectedRessources.ToArray())
                {
                    if (appointment.Ressource != ConstVariables.AppointmentOfRoom && appointment.Ressource != ConstVariables.AppointmentOfClient && appointment.Ressource != ConstVariables.PlannedTreatment && appointment.Ressource != ConstVariables.PlannedCleaning)
                    {
                        treatmentCreationModel.AppointmentsOfSelectedRessources.Remove(appointment);
                    }
                }
            }
            else
            {
                // for adding new appointments (see the steps below) it is necessary that the list is not null
                treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
            }

            // get the appointments of each selected staffmember
            foreach (var staffMember in treatmentCreationModel.SelectedStaff)
            {
                // load the appointments of the selected staffmember from db (don't load appointments where the id fits --> this is important for editing treatments)
                var newStaffAppointments = _db.Treatments.Where(t => t.EndDate > DateTime.Now && t.ApplicationUsers.Any(a => a.Id == staffMember.Id) && t.Id != treatmentCreationModel.Id).ToList();

                // convert these appointments (class treatment) into objects of AppointmentOfSelectedRessource and add them to treatmentCreationModel.AppointmentsOfSelectedRessources
                foreach (var appointment in newStaffAppointments)
                {
                    AppointmentOfSelectedRessource appointmentOfSelectedStaffMember = new AppointmentOfSelectedRessource();
                    appointmentOfSelectedStaffMember.BeginDate = appointment.BeginDate;
                    appointmentOfSelectedStaffMember.EndDate = appointment.EndDate;
                    appointmentOfSelectedStaffMember.Ressource = staffMember.DisplayName;
                    appointmentOfSelectedStaffMember.EventColor = "#FFD700";
                    treatmentCreationModel.AppointmentsOfSelectedRessources.Add(appointmentOfSelectedStaffMember);
                }
            }

            //return ViewModel
            return treatmentCreationModel;
        }

        //[KrabsJ]
        //this method updates the viewModel data of client appointments
        //expected parameter: CreationTreatment viewModel
        //return: CreationTreatment viewModel
        private CreationTreatment UpdateViewModelClientAppointments(CreationTreatment treatmentCreationModel)
        {
            // remove the appointments of the client that was found before
            if (treatmentCreationModel.AppointmentsOfSelectedRessources != null)
            {
                foreach (var appointment in treatmentCreationModel.AppointmentsOfSelectedRessources.ToArray())
                {
                    if (appointment.Ressource == ConstVariables.AppointmentOfClient)
                    {
                        treatmentCreationModel.AppointmentsOfSelectedRessources.Remove(appointment);
                    }
                }
            }
            else
            {
                // for adding new client appointments (see the steps below) it is necessary that the list is not null
                treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
            }

            // get all client appointments from db
            List<AppointmentOfSelectedRessource> clientAppointments = GetClientAppointments(treatmentCreationModel.ClientId, treatmentCreationModel.Id);

            // store the (new) found client appointments in the viewModel
            foreach (var appointment in clientAppointments)
            {
                treatmentCreationModel.AppointmentsOfSelectedRessources.Add(appointment);
            }
            return treatmentCreationModel;
        }

        //[KrabsJ]
        //this method returns all appointments (in the future) of the client
        //expected parameter: int clientId
        //return: List<AppointmentOfSelectedRessource> clientAppointments
        private List<AppointmentOfSelectedRessource> GetClientAppointments(int clientId, int? treatmentId)
        {
            // get all stays of the client that are not finished
            var CurrentClientStays = _db.Stays.Where(s => s.ClientId == clientId && (s.EndDate == null || s.EndDate > DateTime.Now)).ToList();

            //extract the treatments of theese stays that are in the future 
            ICollection<Treatment> ClientTreatments = new Collection<Treatment>();
            foreach (var stay in CurrentClientStays)
            {
                foreach (var treatment in stay.Treatments)
                {
                    if (treatment.EndDate > DateTime.Now && treatment.Id != treatmentId)
                    {
                        ClientTreatments.Add(treatment);
                    }
                }
            }

            //convert the list of treatments to a list of AppointmentsOfSelectedRessource (this class only contains the attributes that are necessary for creating a new treatment)
            List<AppointmentOfSelectedRessource> clientAppointments = new List<AppointmentOfSelectedRessource>();
            foreach (var treatment in ClientTreatments)
            {
                AppointmentOfSelectedRessource appointmentOfClient = new AppointmentOfSelectedRessource();
                appointmentOfClient.BeginDate = treatment.BeginDate;
                appointmentOfClient.EndDate = treatment.EndDate;
                appointmentOfClient.Ressource = ConstVariables.AppointmentOfClient;
                clientAppointments.Add(appointmentOfClient);
            }

            return clientAppointments;
        }

        //[KrabsJ]
        //this method updates the viewModel data depending on the BeginDate and EndDate
        //expected parameter: CreationTreatment viewModel
        //return: CreationTreatment viewModel
        private CreationTreatment UpdateViewModelByPlannedTreatment(CreationTreatment treatmentCreationModel)
        {
            // remove the new treatment that was planned before
            if (treatmentCreationModel.AppointmentsOfSelectedRessources != null)
            {
                foreach (var appointment in treatmentCreationModel.AppointmentsOfSelectedRessources.ToArray())
                {
                    if (appointment.Ressource == ConstVariables.PlannedTreatment || appointment.Ressource == ConstVariables.PlannedCleaning)
                    {
                        treatmentCreationModel.AppointmentsOfSelectedRessources.Remove(appointment);
                    }
                }
            }
            else
            {
                // for adding a new planned treatment (see the steps below) it is necessary that the list is not null
                treatmentCreationModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
            }

            // store a new planned treatment in the viewModel if the time inputs (BeginDate & EndDate) are valid
            if (treatmentCreationModel.BeginDate != null && treatmentCreationModel.EndDate != null && (treatmentCreationModel.BeginDate < treatmentCreationModel.EndDate))
            {
                AppointmentOfSelectedRessource plannedTreatment = new AppointmentOfSelectedRessource()
                {
                    BeginDate = treatmentCreationModel.BeginDate.Value,
                    EndDate = treatmentCreationModel.EndDate.Value,
                    Ressource = ConstVariables.PlannedTreatment,
                    EventColor = "#FF8C00",
                    IsOriginalAppointment = true
                };
                treatmentCreationModel.AppointmentsOfSelectedRessources.Add(plannedTreatment);

                // add an optional planned cleaning
                if (treatmentCreationModel.CleaningDuration != CleaningDuration.noCleaning)
                {
                    var optionalCleaningAppointment = CreateOptionalCleaningAppointment(treatmentCreationModel.EndDate.Value, treatmentCreationModel.CleaningDuration);
                    optionalCleaningAppointment.IsOriginalAppointment = true;
                    treatmentCreationModel.AppointmentsOfSelectedRessources.Add(optionalCleaningAppointment);
                }

                // create optional series events
                if (treatmentCreationModel.SeriesCounter >= 0)
                {
                    switch (treatmentCreationModel.Series)
                    {
                        case Series.noSeries:
                            break;
                        case Series.day:
                            for (int i = 1; i <= treatmentCreationModel.SeriesCounter; i++)
                            {
                                AppointmentOfSelectedRessource plannedSeriesTreatment = new AppointmentOfSelectedRessource()
                                {
                                    BeginDate = treatmentCreationModel.BeginDate.Value.AddDays(i * 1),
                                    EndDate = treatmentCreationModel.EndDate.Value.AddDays(i * 1),
                                    Ressource = ConstVariables.PlannedTreatment,
                                    EventColor = "#FF8C00",
                                    IsOriginalAppointment = false
                                };
                                treatmentCreationModel.AppointmentsOfSelectedRessources.Add(plannedSeriesTreatment);

                                // add an optional planned cleaning
                                if (treatmentCreationModel.CleaningDuration != CleaningDuration.noCleaning)
                                {
                                    var optionalCleaningAppointmentForSeriesTreatment = CreateOptionalCleaningAppointment(plannedSeriesTreatment.EndDate, treatmentCreationModel.CleaningDuration);
                                    optionalCleaningAppointmentForSeriesTreatment.IsOriginalAppointment = false;
                                    treatmentCreationModel.AppointmentsOfSelectedRessources.Add(optionalCleaningAppointmentForSeriesTreatment);
                                }
                            }
                            break;
                        case Series.week:
                            for (int i = 1; i <= treatmentCreationModel.SeriesCounter; i++)
                            {
                                AppointmentOfSelectedRessource plannedSeriesTreatment = new AppointmentOfSelectedRessource()
                                {
                                    BeginDate = treatmentCreationModel.BeginDate.Value.AddDays(i * 7),
                                    EndDate = treatmentCreationModel.EndDate.Value.AddDays(i * 7),
                                    Ressource = ConstVariables.PlannedTreatment,
                                    EventColor = "#FF8C00",
                                    IsOriginalAppointment = false
                                };
                                treatmentCreationModel.AppointmentsOfSelectedRessources.Add(plannedSeriesTreatment);

                                // add an optional planned cleaning
                                if (treatmentCreationModel.CleaningDuration != CleaningDuration.noCleaning)
                                {
                                    var optionalCleaningAppointmentForSeriesTreatment = CreateOptionalCleaningAppointment(plannedSeriesTreatment.EndDate, treatmentCreationModel.CleaningDuration);
                                    optionalCleaningAppointmentForSeriesTreatment.IsOriginalAppointment = false;
                                    treatmentCreationModel.AppointmentsOfSelectedRessources.Add(optionalCleaningAppointmentForSeriesTreatment);
                                }
                            }
                            break;
                        case Series.twoWeeks:
                            for (int i = 1; i <= treatmentCreationModel.SeriesCounter; i++)
                            {
                                AppointmentOfSelectedRessource plannedSeriesTreatment = new AppointmentOfSelectedRessource()
                                {
                                    BeginDate = treatmentCreationModel.BeginDate.Value.AddDays(i * 14),
                                    EndDate = treatmentCreationModel.EndDate.Value.AddDays(i * 14),
                                    Ressource = ConstVariables.PlannedTreatment,
                                    EventColor = "#FF8C00",
                                    IsOriginalAppointment = false
                                };
                                treatmentCreationModel.AppointmentsOfSelectedRessources.Add(plannedSeriesTreatment);

                                // add an optional planned cleaning
                                if (treatmentCreationModel.CleaningDuration != CleaningDuration.noCleaning)
                                {
                                    var optionalCleaningAppointmentForSeriesTreatment = CreateOptionalCleaningAppointment(plannedSeriesTreatment.EndDate, treatmentCreationModel.CleaningDuration);
                                    optionalCleaningAppointmentForSeriesTreatment.IsOriginalAppointment = false;
                                    treatmentCreationModel.AppointmentsOfSelectedRessources.Add(optionalCleaningAppointmentForSeriesTreatment);
                                }
                            }
                            break;
                        case Series.month:
                            for (int i = 1; i <= treatmentCreationModel.SeriesCounter; i++)
                            {
                                AppointmentOfSelectedRessource plannedSeriesTreatment = new AppointmentOfSelectedRessource()
                                {
                                    BeginDate = treatmentCreationModel.BeginDate.Value.AddDays(i * 28),
                                    EndDate = treatmentCreationModel.EndDate.Value.AddDays(i * 28),
                                    Ressource = ConstVariables.PlannedTreatment,
                                    EventColor = "#FF8C00",
                                    IsOriginalAppointment = false
                                };
                                treatmentCreationModel.AppointmentsOfSelectedRessources.Add(plannedSeriesTreatment);

                                // add an optional planned cleaning
                                if (treatmentCreationModel.CleaningDuration != CleaningDuration.noCleaning)
                                {
                                    var optionalCleaningAppointmentForSeriesTreatment = CreateOptionalCleaningAppointment(plannedSeriesTreatment.EndDate, treatmentCreationModel.CleaningDuration);
                                    optionalCleaningAppointmentForSeriesTreatment.IsOriginalAppointment = false;
                                    treatmentCreationModel.AppointmentsOfSelectedRessources.Add(optionalCleaningAppointmentForSeriesTreatment);
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                
            }

            return treatmentCreationModel;
        }

        // [KrabsJ]
        // this method creates an cleaning appointment
        // expected parameters: DateTime endOfPlannedTreatment, CleaningDuration plannedCleaningDuration
        // return: AppointmentOfSelectedRessource plannedCleaning
        private AppointmentOfSelectedRessource CreateOptionalCleaningAppointment(DateTime endOfPlannedTreatment, CleaningDuration plannedCleaningDuration)
        {
            AppointmentOfSelectedRessource plannedCleaning = new AppointmentOfSelectedRessource();
            switch (plannedCleaningDuration)
            {
                case CleaningDuration.noCleaning:
                    break;
                case CleaningDuration.tenMinutes:
                    plannedCleaning.BeginDate = endOfPlannedTreatment;
                    plannedCleaning.EndDate = endOfPlannedTreatment.AddMinutes(10);
                    plannedCleaning.Ressource = ConstVariables.PlannedCleaning;
                    plannedCleaning.EventColor = "#FF8C00";
                    break;
                case CleaningDuration.twentyMinutes:
                    plannedCleaning.BeginDate = endOfPlannedTreatment;
                    plannedCleaning.EndDate = endOfPlannedTreatment.AddMinutes(20);
                    plannedCleaning.Ressource = ConstVariables.PlannedCleaning;
                    plannedCleaning.EventColor = "#FF8C00";
                    break;
                case CleaningDuration.thirtyMinutes:
                    plannedCleaning.BeginDate = endOfPlannedTreatment;
                    plannedCleaning.EndDate = endOfPlannedTreatment.AddMinutes(30);
                    plannedCleaning.Ressource = ConstVariables.PlannedCleaning;
                    plannedCleaning.EventColor = "#FF8C00";
                    break;
                default:
                    break;
            }

            return plannedCleaning;
        }

        // GET: Treatment/Details [KrabsJ]
        // this method returns the view "Details" that shows the details data of a treatment
        // expected parameter: int treatmentId
        // return: view(Treatment treatment)
        public ActionResult Details(int Id)
        {
            // get treatment from db
            var treatment = _db.Treatments.Single(t => t.Id == Id);

            // create viewModel
            DetailsTreatment treatmentDetails = new DetailsTreatment
            {
                Treatment = treatment,
                DeleteOption = TreatmentSeriesDeleteOptions.onlySelectedTreatment
            };

            //return view
            return View(treatmentDetails);
        }

        // POST: Treatment/Delete [KrabsJ]
        // this method deletes a single treatment
        // expected parameter: int treatmentId
        // return: redirectToAction("Details", "Stay", int stayId)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int Id)
        {
            // get treatment that will be deleted
            var deleteTreatment = _db.Treatments.Single(t => t.Id == Id);
            // save stayId for return value
            var stayId = deleteTreatment.StayId;

            // get an associated cleaning event if existing
            var deleteCleaning = _db.Cleaner.SingleOrDefault(c => c.TreatmentId == deleteTreatment.Id);

            // if an cleaning event exists, delete it first, so that there are no dependencies on the treatment
            if (deleteCleaning != null)
            {
                _db.Cleaner.Remove(deleteCleaning);
                _db.SaveChanges();
            }

            // delete the treatment
            _db.Treatments.Remove(deleteTreatment);
            _db.SaveChanges();

            // success-message for alert-statement
            TempData["DeleteTreatmentSuccess"] = " Die Behandlung wurde erfolgreich gelöscht.";

            // redirect to detailspage of stay
            return RedirectToAction("Details", "Stay", new { id = stayId });
        }

        // POST: Treatment/Delete [KrabsJ]
        // this method deletes one or many treatments of a series, depending on the selected option
        // expected parameter: DetailsTreatment treatmentDetails
        // return: redirectToAction("Details", "Stay", int stayId)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteWithOption(DetailsTreatment treatmentDetails)
        {
            // save stayId for return value
            var stayId = treatmentDetails.Treatment.StayId;

            // get treatments that will be deleted depending on the selected delete option
            List<Treatment> deleteTreatmentList = new List<Treatment>();
            switch (treatmentDetails.DeleteOption)
            {
                case TreatmentSeriesDeleteOptions.onlySelectedTreatment:
                    // call delete method, which deletes a single treatment
                    return Delete(treatmentDetails.Treatment.Id);
                case TreatmentSeriesDeleteOptions.selectedAndFutureTreatments:
                    deleteTreatmentList = _db.Treatments.Where(t => t.IdOfSeries != null && t.IdOfSeries == treatmentDetails.Treatment.IdOfSeries && t.BeginDate >= treatmentDetails.Treatment.BeginDate).ToList();
                    break;
                case TreatmentSeriesDeleteOptions.allTreatments:
                    deleteTreatmentList = _db.Treatments.Where(t => t.IdOfSeries != null && t.IdOfSeries == treatmentDetails.Treatment.IdOfSeries).ToList();
                    break;
                default:
                    break;
            }

            // get associated cleaning events if existing
            List<Cleaner> deleteCleaningsList = new List<Cleaner>();
            foreach (var treatment in deleteTreatmentList)
            {
                var deleteCleaning = _db.Cleaner.SingleOrDefault(c => c.TreatmentId == treatment.Id);
                if (deleteCleaning != null)
                {
                    deleteCleaningsList.Add(deleteCleaning);
                }

            }

            // delete Cleanings first, so that there are no dependencies on the treatment
            _db.Cleaner.RemoveRange(deleteCleaningsList);
            _db.SaveChanges();

            // delete treatments
            _db.Treatments.RemoveRange(deleteTreatmentList);
            _db.SaveChanges();

            // success-message for alert-statement
            TempData["DeleteTreatmentSuccess"] = " Die Behandlungen wurde erfolgreich gelöscht.";

            return RedirectToAction("Details", "Stay", new { Id = stayId });
        }

        // GET: Treatment/Edit [KrabsJ]
        // this method returns the view "Create" that enables the user to edit an existing treatment
        // expected parameter: int treatmentId
        // return: view("Create", CreationTreatment viewModel)
        public ActionResult Edit(int Id)
        {
            // get treatment from db
            var treatment = _db.Treatments.Single(t => t.Id == Id);

            // create viewModel
            CreationTreatment treatmentEditModel = new CreationTreatment
            {
                Id = treatment.Id,
                StayId = treatment.StayId,
                ClientId = treatment.Stay.ClientId,
                ClientName = treatment.Stay.Client.Surname + " " + treatment.Stay.Client.Lastname,
                TreatmentTypeId = treatment.TreatmentTypeId,
                TreatmentTypeName = treatment.TreatmentType.Name,
                BeginDate = treatment.BeginDate,
                EndDate = treatment.EndDate,
                Description = treatment.Description,
                SelectedRoomId = treatment.RoomId,
                SelectedRoomNumber = treatment.Room.RoomNumber,
                ShowCalendarDay = treatment.BeginDate.ToString("s"),
        };

            //load the rooms that are available for the selectedTreatmentType
            ICollection<Room> rooms = new Collection<Room>();
            if (treatment.TreatmentType.RoomTypeId == null)
            {
                rooms = _db.Rooms.ToList();
            }
            else
            {
                rooms = _db.Rooms.Where(r => r.RoomTypeId == treatment.TreatmentType.RoomTypeId).ToList();
            }

            //convert the list of rooms to a list of SelectionRooms (this class only contains the attributes that are necessary for creating a new treatment)
            treatmentEditModel.Rooms = new List<SelectionRoom>();
            foreach (var item in rooms)
            {
                SelectionRoom selectionRoom = new SelectionRoom();
                selectionRoom.Id = item.Id;
                selectionRoom.RoomNumber = item.RoomNumber;
                selectionRoom.RoomTypeName = item.RoomType.Name;
                treatmentEditModel.Rooms.Add(selectionRoom);
            }

            // get all users (besides cleaners & adminstrators) from the db
            ICollection<ApplicationUser> userList = new Collection<ApplicationUser>();
            userList = _db.Users.Where(u => u.UserType.ToString() != ConstVariables.RoleCleaner && u.UserType.ToString() != ConstVariables.RoleAdministrator).ToList();

            // convert the list of users into a list of staffmembers and store already seleted staff members in the associated list
            treatmentEditModel.Staff = new List<Staff>();
            treatmentEditModel.SelectedStaff = new List<Staff>();
            foreach (var item in userList)
            {
                Staff staffMember = new Staff();
                staffMember.Id = item.Id;
                if (item.Title == null)
                {
                    staffMember.DisplayName = item.Surname + " " + item.Lastname;
                }
                else
                {
                    staffMember.DisplayName = item.Title + " " + item.Surname + " " + item.Lastname;
                }
                staffMember.StaffType = item.UserType;
                if (treatment.ApplicationUsers.Any(u => u.Id == staffMember.Id))
                {
                    staffMember.Selected = true;
                    treatmentEditModel.SelectedStaff.Add(staffMember);
                }
                else
                {
                    staffMember.Selected = false;
                }
                treatmentEditModel.Staff.Add(staffMember);
            }

            // get an optional cleaning event
            Cleaner associatedCleaning = _db.Cleaner.SingleOrDefault(c => c.TreatmentId == treatment.Id);
            if (associatedCleaning != null)
            {
                treatmentEditModel.CleaningDuration = associatedCleaning.CleaningDuration;
                treatmentEditModel.CleaningId = associatedCleaning.Id;
            }
            else
            {
                treatmentEditModel.CleaningDuration = CleaningDuration.noCleaning;
                treatmentEditModel.CleaningId = null;
            }

            // check if treatment has got a series
            if (treatment.IdOfSeries != null)
            {
                treatmentEditModel.IsStoredWithSeries = true;
                treatmentEditModel.IdOfSeries = treatment.IdOfSeries;
            }
            else
            {
                treatmentEditModel.IsStoredWithSeries = false;
                treatmentEditModel.IdOfSeries = null;
            }
            //treatmentEditModel.Series = Series.noSeries and treatmentEditModel.SeriesCounter = 0 are the initial values for planing series events
            //but it is also important that these values are set, if a treatment already is part of a series because these attribute ensures that the method "UpdateViewModelByPlannedTreatment" doesn't create further series object
            //there is no possibilty to edit a whole series --> it is only possible to a single treatment
            //if needed the user can delete a whole series and create a new one
            treatmentEditModel.Series = Series.noSeries;
            treatmentEditModel.SeriesCounter = 0;

            // update the viewModelDate --> this means especially creating data for AppointmentsOfSelectedRessources
            treatmentEditModel = UpdateViewModelByPlannedTreatment(treatmentEditModel);
            treatmentEditModel = UpdateViewModelByRoomSelection(treatmentEditModel);
            treatmentEditModel = UpdateViewModelByStaffSelection(treatmentEditModel);
            treatmentEditModel = UpdateViewModelClientAppointments(treatmentEditModel);

            // create JsonResult for calendar in view (therefore it is necessary that the list of appointments is not null)
            if (treatmentEditModel.AppointmentsOfSelectedRessources == null)
            {
                treatmentEditModel.AppointmentsOfSelectedRessources = new List<AppointmentOfSelectedRessource>();
            }
            treatmentEditModel.JsonAppointmentsOfSelectedRessources = CreateJsonResult(treatmentEditModel.AppointmentsOfSelectedRessources);

            //return view
            return View("Create", treatmentEditModel);
        }

        //helper method for creating the JsonResult, this is required for the calendar in the create-view [KrabsJ]
        private JsonResult CreateJsonResult(IList<AppointmentOfSelectedRessource> appointmentList)
        {
            //Builds a JSon from the appointmentList
            var result = appointmentList.Select(a => new JsonEventTreatment()
            {
                start = a.BeginDate.ToString("s"),
                end = a.EndDate.ToString("s"),
                title = a.Ressource,
                color = a.EventColor,
            }).ToList();

            //Creates a JsonResult from the Json
            JsonResult resultJson = new JsonResult { Data = result };

            return resultJson;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _db.Dispose();
            base.Dispose(disposing);
        }
    }
}