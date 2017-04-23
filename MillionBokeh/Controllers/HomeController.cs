using MillionBokeh.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MillionBokeh.Controllers
{
    public class HomeController : Controller
    {
        private MillionBokehEntities db = new MillionBokehEntities();

        public ActionResult Index()
        {

            // Populate the model for the content of the page using the HomeViewModel because it contains all the fields we need
            // to populate the home page.
            HomeViewModel model = new HomeViewModel();
            var contents = db.SiteContents.ToList();
            model.AboutUs1 = contents.Where(c => c.ContentType == "AboutUs1").Select(c => c.Content).First();

            model.Card1Title = contents.Where(c => c.ContentType == "Card1Title").Select(c => c.Content).First();
            model.Card1Text = contents.Where(c => c.ContentType == "Card1Text").Select(c => c.Content).First();
            model.Card1Link = contents.Where(c => c.ContentType == "Card1Link").Select(c => c.Content).First();

            model.Card2Title = contents.Where(c => c.ContentType == "Card2Title").Select(c => c.Content).First();
            model.Card2Text = contents.Where(c => c.ContentType == "Card2Text").Select(c => c.Content).First();
            model.Card2Image = contents.Where(c => c.ContentType == "Card2Image").Select(c => c.Content).First();

            model.Card3Link = contents.Where(c => c.ContentType == "Card3Link").Select(c => c.Content).First();
            model.Card3Title = contents.Where(c => c.ContentType == "Card3Title").Select(c => c.Content).First();
            model.Card3Text1 = contents.Where(c => c.ContentType == "Card3Text1").Select(c => c.Content).First();
            model.Card3Text2 = contents.Where(c => c.ContentType == "Card3Text2").Select(c => c.Content).First();

            model.Card4Title = contents.Where(c => c.ContentType == "Card4Title").Select(c => c.Content).First();
            model.Card4Text1 = contents.Where(c => c.ContentType == "Card4Text1").Select(c => c.Content).First();
            model.Card4Text2 = contents.Where(c => c.ContentType == "Card4Text2").Select(c => c.Content).First();

            model.Card5Text1 = contents.Where(c => c.ContentType == "Card5Text1").Select(c => c.Content).First();
            model.Card5Text2 = contents.Where(c => c.ContentType == "Card5Text2").Select(c => c.Content).First();

            model.Card6Title = contents.Where(c => c.ContentType == "Card6Title").Select(c => c.Content).First();
            model.Card6Text1 = contents.Where(c => c.ContentType == "Card6Text1").Select(c => c.Content).First();
            model.Card6Text2 = contents.Where(c => c.ContentType == "Card6Text2").Select(c => c.Content).First();
            model.Card6Image = contents.Where(c => c.ContentType == "Card6Image").Select(c => c.Content).First();

            model.Card7Title = contents.Where(c => c.ContentType == "Card7Title").Select(c => c.Content).First();
            model.Card7Text1 = contents.Where(c => c.ContentType == "Card7Text1").Select(c => c.Content).First();
            model.Card7Text2 = contents.Where(c => c.ContentType == "Card7Text2").Select(c => c.Content).First();

            model.ContactPhone = contents.Where(c => c.ContentType == "ContactPhone").Select(c => c.Content).First();
            model.ContactMail = contents.Where(c => c.ContentType == "ContactMail").Select(c => c.Content).First();
            model.ContactAddress = contents.Where(c => c.ContentType == "ContactAddress").Select(c => c.Content).First();


            return View(model);
        }

        // Used to edit certain parts of the home page
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string Edit(string propertyName, string propertyvalue)
        {
            var contentToUpdate = db.SiteContents.Where(c => c.ContentType == propertyName).First();
            contentToUpdate.Content = propertyvalue;
            db.SaveChanges();
            return propertyvalue;
        }


        // Uploads an image to the site directory. This is used to replace the images on the home page's cards
        public string UploadImage(string contenttype)
        {
            var fileName = "";
            if (System.Web.HttpContext.Current.Request.Files.AllKeys.Any())
            {
                var pic = System.Web.HttpContext.Current.Request.Files["Image"];
                fileName = Path.GetFileName(contenttype + Path.GetExtension(pic.FileName));
                var path = Path.Combine(Server.MapPath("~/images/site/"), fileName);
                pic.SaveAs(path);

                var contentToUpdate = db.SiteContents.Where(c => c.ContentType == contenttype).First();
                contentToUpdate.Content = fileName;
                db.SaveChanges();
            }
            return fileName;
        }
    }
}