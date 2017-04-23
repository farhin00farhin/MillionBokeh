using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using MillionBokeh.Models;
using Newtonsoft.Json;
using System.IO;
using System.Web.UI.WebControls;

namespace MillionBokeh.Controllers
{
    public class EventsController : Controller
    {
        private MillionBokehEntities db = new MillionBokehEntities();

        // 20170313 SA Returns the list of events in the database according to the criteria (month or search term)
        public ActionResult Index(string month, string term, string pagenumber)
        {
            if (string.IsNullOrWhiteSpace(Utilities.GetLoggedInUserId(User)))
            {
                return Redirect("/account/login");
            }

            ViewBag.CurrentMenu = "events";
            ViewBag.Month = month;
            ViewBag.Term = term;
            ViewBag.CurrentPage = pagenumber;

            IEnumerable<Event> eventsToReturn = db.Events;
            eventsToReturn = ApplySort(eventsToReturn);
            eventsToReturn = FilterByMonth(eventsToReturn);
            eventsToReturn = FilterByTerm(eventsToReturn);
            eventsToReturn = GetOnlyCurrentPage(eventsToReturn);

            GeneratePageUrls();

            return View(eventsToReturn.ToList());
        }
        private void GeneratePageUrls()
        {
            ViewBag.Month = 0;
            if (Request.Params["month"] != null && !string.IsNullOrWhiteSpace(Request.Params["month"]))
            {
                ViewBag.Month = Convert.ToInt32(Request.Params["month"]);
            }

            if (Request.Params["term"] != null && !string.IsNullOrWhiteSpace(Request.Params["term"]))
            {
                ViewBag.Term = Convert.ToString(Request.Params["term"]);
            }

            ViewBag.PageNumberUrl = string.Format("?month={0}&term={1}&PageNumber={2}", ViewBag.Month, ViewBag.Term, "{0}");

        }

        private IEnumerable<Event> FilterByTerm(IEnumerable<Event> fullEventList)
        {
            if (!string.IsNullOrWhiteSpace(ViewBag.Term))
            {
                string term = Convert.ToString(ViewBag.Term);
                return fullEventList.Where(e => e.Name.Contains(term) || e.Description.Contains(term));
            }

            return fullEventList;
        }

        private IEnumerable<Event> FilterByMonth(IEnumerable<Event> fullEventList)
        {
            if (!string.IsNullOrWhiteSpace(ViewBag.Month))
            {
                int monthInt = Convert.ToInt32(ViewBag.Month);
                if (monthInt > 0 && monthInt <= 12)
                {
                    ViewBag.Month = monthInt;
                    fullEventList = fullEventList.Where(e => e.StartDate.Month == (monthInt)).ToList();
                }
            }

            return fullEventList;
        }

        private IEnumerable<Event> ApplySort(IEnumerable<Event> fullEventList)
        {
            return fullEventList.OrderBy(e => e.StartDate);
        }


        private IEnumerable<Event> GetOnlyCurrentPage(IEnumerable<Event> fullList)
        {
            ViewBag.TotalNum = fullList.Count();
            UpdatePageNumbers();
            int eventsPerPage = 9;
            int pageNumber = ViewBag.CurrentPage;
            return fullList.Skip((pageNumber - 1) * eventsPerPage).Take(eventsPerPage);
        }

        private void UpdatePageNumbers()
        {
            int pageNumber = 1;
            if (!string.IsNullOrWhiteSpace(Request.Params["PageNumber"]))
            {
                pageNumber = Convert.ToInt32(Request.Params["PageNumber"]);
            }
            ViewBag.CurrentPage = pageNumber;
            ViewBag.NextPage = pageNumber >= (Math.Ceiling((double)(ViewBag.TotalNum / 9.0))) ? pageNumber : pageNumber + 1;
            ViewBag.PreviousPage = pageNumber <= 1 ? 1 : pageNumber - 1;

            ViewBag.PreviousPageDisabled = ViewBag.CurrentPage == ViewBag.PreviousPage ? " disabled fade" : "";
            ViewBag.NextPageDisabled = ViewBag.CurrentPage == ViewBag.NextPage ? "disabled fade" : "";
        }

        public ActionResult Details(int id)
        {
            Event @event = db.Events.Find(id);
            if (@event == null)
            {
                return HttpNotFound();
            }
            ViewBag.CurrentMenu = "events";
            return View(@event);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create()
        {
            // 20170326 SA New events are created with some default values. The admin will go and change these values as soon as they create it.
            Event newEvent = new Event()
            {
                Duration = 0,
                Description = "Enter event description here",
                Image = "No_image_available.png",
                Fee = 0M,
                Location = "Enter event location",
                Name = "New Event",
                Organiser = "Enter event organiser name",
                StartDate = DateTime.Now
            };

            db.Events.Add((newEvent));
            db.SaveChanges();

            return Redirect("/events/details?id=" + newEvent.EventID);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string Edit(int id, string propertyName, string propertyvalue)
        {
            var eventToUpdate = db.Events.Find(id);
            System.Reflection.PropertyInfo prop = typeof(Event).GetProperty(propertyName);

            if (prop.PropertyType == typeof(string))
            {
                prop.SetValue(eventToUpdate, propertyvalue);
            }
            else if (prop.PropertyType == typeof(decimal))
            {
                prop.SetValue(eventToUpdate, Convert.ToDecimal(propertyvalue));
            }
            else if (prop.PropertyType == typeof(short))
            {
                prop.SetValue(eventToUpdate, Convert.ToInt16(propertyvalue));
            }
            else if (prop.PropertyType == typeof(int))
            {
                prop.SetValue(eventToUpdate, Convert.ToInt32(propertyvalue));
            }
            else if (prop.PropertyType == typeof(DateTime))
            {
                DateTime toSet = Convert.ToDateTime(propertyvalue);
                prop.SetValue(eventToUpdate, toSet);
            }
            db.SaveChanges();

            return propertyvalue;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            Event eve = db.Events.Find(id);
            db.Events.Remove(eve);
            db.SaveChanges();
            return RedirectToAction("Index");
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        public string AutoComplete(string term)
        {
            var foundEvents = db.Events.Where(n => n.Name.Contains(term)).Select(n => n.Name).Take(5).ToList();
            return JsonConvert.SerializeObject(foundEvents);
        }


        public string UploadImage(int id)
        {
            var fileName = "";
            if (System.Web.HttpContext.Current.Request.Files.AllKeys.Any())
            {
                var pic = System.Web.HttpContext.Current.Request.Files["Image"];
                fileName = Path.GetFileName(id + Path.GetExtension(pic.FileName));
                var path = Path.Combine(Server.MapPath("~/images/events/"), fileName);
                pic.SaveAs(path);


                var eventToUpdate = db.Events.Find(id);
                eventToUpdate.Image = fileName;
                db.SaveChanges();

            }
            return fileName;
        }
    }
}
