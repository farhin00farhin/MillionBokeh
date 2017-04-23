using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using MillionBokeh.Models;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security.DataHandler;
using MillionBokeh.ViewModels;

namespace MillionBokeh.Controllers
{
    public class MembersController : Controller
    {
        private MillionBokehEntities db = new MillionBokehEntities();

        private ApplicationUserManager _userManager;

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public ActionResult Index(string term, string range, string pagenumber, string findphotographer, int[] regions)
        {
            if (string.IsNullOrWhiteSpace(Utilities.GetLoggedInUserId(User)))
            {
                return Redirect("/account/login");
            }

            if (!Utilities.IsUserAdmin(User) && !Utilities.IsMembershipValid(Utilities.GetMember(User)))
            {
                return Redirect("/members/benefits/");
            }

            ViewBag.Term = term;
            ViewBag.Range = range;
            ViewBag.PageNumber = pagenumber;
            ViewBag.FindPhotographer = findphotographer;
            ViewBag.Locations = regions;

            // 20170317 SA Filter using LINQ step by step to return members according to the scope
            IEnumerable<AspNetUser> usersToReturn = db.AspNetUsers;
            usersToReturn = ApplySort(usersToReturn);
            usersToReturn = FilterByMembership(usersToReturn);
            usersToReturn = FilterByName(usersToReturn);
            usersToReturn = FilterByRange(usersToReturn);
            usersToReturn = FilterByLocations(usersToReturn);
            usersToReturn = GetOnlyCurrentPage(usersToReturn);

            // 20170317 SA Help the page to display the correct links
            GeneratePageUrls();

            var locationsList = db.Locations.ToList();

            return View(new MembersIndexViewModel() { Users = usersToReturn.ToList(), Locations = locationsList });
        }

        #region Filtering and sorting


        // 20170317 SA Filter the list of photographers by the price range
        private IEnumerable<AspNetUser> FilterByRange(IEnumerable<AspNetUser> fullUserList)
        {
            if (!string.IsNullOrWhiteSpace(ViewBag.Range))
            {
                int rangeInt = Convert.ToInt32(ViewBag.Range);
                if (rangeInt > 0)
                {
                    ViewBag.Range = rangeInt;
                    return fullUserList.Where(e => e.RangeId == rangeInt);
                }
            }

            return fullUserList;
        }        
        
        // 20170411 SA Filter the list of photographers by their locations
        private IEnumerable<AspNetUser> FilterByLocations(IEnumerable<AspNetUser> fullUserList)
        {
            if (ViewBag.Locations != null)
            {
                int[] locationsInts = (int[])(ViewBag.Locations);
                if (locationsInts.Length > 0)
                {
                    ViewBag.Locations = locationsInts;
                    var userIdsInLocations = db.UserLocations.Where(x => locationsInts.Contains(x.LocationId)).Select(x => x.UserId);

                    // 20170412 SA This is what I will be returning as my search results
                    fullUserList = fullUserList.Where(u => userIdsInLocations.Contains(u.Id));

                    // 20170412 SA I need to get the locations from my search results
                    var locations = db.UserLocations.Where(x => userIdsInLocations.Contains(x.UserId)).Select(l => l.Location);

                    // 20170412 SA I need to get only the locations that the user is ineterested in
                    locations = locations.Where(l => locationsInts.Contains(l.Id));
                    if (locations.Any())
                    {
                        // 20170412 SA I need to format the locations in this format so it's a valid javascript array: "['Bondi Beach', '-33.890542', '151.274856'],['Coogee Beach', '-33.923036', '151.259052']"
                        StringBuilder builder = new StringBuilder();
                        foreach (var loc in locations)
                        {
                            var photographerList = db.UserLocations.Where(ul => ul.LocationId == loc.Id).Select(ul => ul.AspNetUser);
                            string[] phtographersInLocation = FilterByMembership(photographerList).OrderBy(x => x.Name).Select(x => x.Name).ToArray();

                            //string[] phtographersInLocation = db.UserLocations.Where(ul => ul.LocationId == loc.Id).OrderBy(ul => ul.AspNetUser.Name).Select(ul => ul.AspNetUser.Name).ToArray();
                            string phtographersInLocationString = string.Join(", ", phtographersInLocation);
                            builder.Append(string.Format("['{0}', '{1}', '{2}'],", phtographersInLocationString, loc.Latitude.ToString("0.000000"), loc.Longitude.ToString("0.000000")));
                        }

                        // 20170412 SA Get rid of the last comma
                        ViewBag.LocationsToDisplay = builder.ToString().Substring(0, builder.Length - 1);
                    }
                }
            }

            return fullUserList;
        }

        // 20170317 SA Filter the list of users by whether they have membership
        private IEnumerable<AspNetUser> FilterByMembership(IEnumerable<AspNetUser> fullUserList)
        {
            // 20170318 SA Member types
            // 2 = Associate member
            // 3 = Full member

            // 20170318 SA return only members with a membership that had been renwed less than a year ago.
            fullUserList = fullUserList.Where(e => e.LastRenewalDate != null && (e.MemberType != null && e.MemberType > 1 &&
                                                                                 (DateTime.Now - e.LastRenewalDate).Value.TotalDays <= 365));

            if (!string.IsNullOrWhiteSpace(ViewBag.FindPhotographer) && Convert.ToBoolean(ViewBag.FindPhotographer))
            {
                fullUserList = fullUserList.Where(e => e.Photos.Count > 0);
            }
            return fullUserList;
        }

        // 20170317 SA Filter the list of users by their name or profile description. This is used to filter by the search term.
        private IEnumerable<AspNetUser> FilterByName(IEnumerable<AspNetUser> fullUserList)
        {
            if (!string.IsNullOrWhiteSpace(ViewBag.Term))
            {
                string term = Convert.ToString(ViewBag.Term);
                return fullUserList.Where(e => e.Name.Contains(term) || (e.Description != null && e.Description.Contains(term)));
            }

            return fullUserList;
        }

        // 20170317 SA Filter the list of users by the page that the user is on. Only return 9 results.
        private IEnumerable<AspNetUser> GetOnlyCurrentPage(IEnumerable<AspNetUser> fullUserList)
        {
            ViewBag.TotalNum = fullUserList.Count();
            UpdatePageNumbers();
            int itemsPerPage = 9;
            int pageNumber = ViewBag.CurrentPage;
            return fullUserList.Skip((pageNumber - 1) * itemsPerPage).Take(itemsPerPage);
        }

        // 20170317 SA Sort the list of users by name
        private IEnumerable<AspNetUser> ApplySort(IEnumerable<AspNetUser> fullUserList)
        {
           
            fullUserList = fullUserList.OrderBy(u => u.Name);
            return fullUserList;
        }

        #endregion

        #region Display helper methods

        // 20170317 SA Update the page numbers according to the state of the page.
        private void UpdatePageNumbers()
        {
            // 20170317 SA Initialise to page one, no matter what.
            int pageNumber = 1;

            // 20170317 SA If there is a page number given in the Request, then it must be the current page the user wants.
            if (Request.Params["PageNumber"] != null && !string.IsNullOrWhiteSpace(Request.Params["PageNumber"]))
            {
                pageNumber = Convert.ToInt32(Request.Params["PageNumber"]);
            }

            // 20170317 SA Calculated the URLs for the paging button according to the page that the user is on.
            ViewBag.CurrentPage = pageNumber;
            ViewBag.NextPage = pageNumber >= (Math.Ceiling((double)(ViewBag.TotalNum / 9.0))) ? pageNumber : pageNumber + 1;
            ViewBag.PreviousPage = pageNumber <= 1 ? 1 : pageNumber - 1;

            // 20170317 SA Disable and hide the next and/or previous buttons if the user is on an edge page.
            ViewBag.PreviousPageDisabled = ViewBag.CurrentPage == ViewBag.PreviousPage ? "disabled fade" : "";
            ViewBag.NextPageDisabled = ViewBag.CurrentPage == ViewBag.NextPage ? "disabled fade" : "";
        }

        // 20170317 SA Generate the page number URL format based on the current ViewBag State. This ensures that the state of the page will be kept in every button on the pager.
        private void GeneratePageUrls()
        {
            ViewBag.Range = 0;
            if (Request.Params["range"] != null && !string.IsNullOrWhiteSpace(Request.Params["range"]))
            {
                ViewBag.Range = Convert.ToInt32(Request.Params["range"]);
            }

            if (Request.Params["name"] != null && !string.IsNullOrWhiteSpace(Request.Params["name"]))
            {
                ViewBag.Name = Convert.ToString(Request.Params["name"]);
            }

            // 20170317 SA Keep a {0} for the page number, because it will be different for every button on the pager.
            ViewBag.PageNumberUrl = string.Format("?name={0}&range={1}&PageNumber={2}&findphotographer={3}", ViewBag.Name, ViewBag.Range, "{0}", ViewBag.FindPhotographer);
        }
        #endregion



        // 20170317 SA Update the content for the member details page. 
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string EditText(string propertyName, string propertyvalue)
        {
            var contentToUpdate = db.SiteContents.Where(c => c.ContentType == propertyName).First();
            contentToUpdate.Content = propertyvalue;
            db.SaveChanges();
            return propertyvalue;
        }

        // 20170402 SA Populate the model for the content of the page using the HomeViewModel because it contains all the fields we need
        // to populate the Benefits page.
        public ActionResult Benefits()
        {
            HomeViewModel model = new HomeViewModel();
            var contents = db.SiteContents.ToList();
            model.RenewCard1Text = contents.Where(c => c.ContentType == "RenewCard1Text").Select(c => c.Content).First();
            model.RenewCard1Title = contents.Where(c => c.ContentType == "RenewCard1Title").Select(c => c.Content).First();
            model.RenewCard2Text = contents.Where(c => c.ContentType == "RenewCard2Text").Select(c => c.Content).First();
            model.RenewCard2Title = contents.Where(c => c.ContentType == "RenewCard2Title").Select(c => c.Content).First();
            model.RenewCard3Text = contents.Where(c => c.ContentType == "RenewCard3Text").Select(c => c.Content).First();
            model.RenewCard3Title = contents.Where(c => c.ContentType == "RenewCard3Title").Select(c => c.Content).First();
            model.RenewCard4Text = contents.Where(c => c.ContentType == "RenewCard4Text").Select(c => c.Content).First();
            model.RenewCard4Title = contents.Where(c => c.ContentType == "RenewCard4Title").Select(c => c.Content).First();
            model.RenewCard5Text = contents.Where(c => c.ContentType == "RenewCard5Text").Select(c => c.Content).First();
            model.RenewCard5Title = contents.Where(c => c.ContentType == "RenewCard5Title").Select(c => c.Content).First();
            model.RenewCard6Text = contents.Where(c => c.ContentType == "RenewCard6Text").Select(c => c.Content).First();
            model.RenewCard6Title = contents.Where(c => c.ContentType == "RenewCard6Title").Select(c => c.Content).First();
            model.RenewCard7Text = contents.Where(c => c.ContentType == "RenewCard7Text").Select(c => c.Content).First();
            model.RenewCard7Title = contents.Where(c => c.ContentType == "RenewCard7Title").Select(c => c.Content).First();
            model.RenewCard8Text = contents.Where(c => c.ContentType == "RenewCard8Text").Select(c => c.Content).First();
            model.RenewCard8Title = contents.Where(c => c.ContentType == "RenewCard8Title").Select(c => c.Content).First();
            model.RenewCard9Text = contents.Where(c => c.ContentType == "RenewCard9Text").Select(c => c.Content).First();
            model.RenewCard9Title = contents.Where(c => c.ContentType == "RenewCard9Title").Select(c => c.Content).First();
            model.RenewCard10Text = contents.Where(c => c.ContentType == "RenewCard10Text").Select(c => c.Content).First();
            return View(model);
        }

        // 20170329 SA Populate the model for the content of the page using the HomeViewModel because it contains all the fields we need
        // to populate the Renew page.
        public ActionResult Renew()
        {
            HomeViewModel model = new HomeViewModel();
            var contents = db.SiteContents.ToList();
            model.RenewText = contents.Where(c => c.ContentType == "RenewText").Select(c => c.Content).First();

            return View(model);
        }

        //  20170329 SA Populate the model for the content of the page using the HomeViewModel because it contains all the fields we need
        // to populate the Refer page.
        public ActionResult Refer()
        {
            HomeViewModel model = new HomeViewModel();
            var contents = db.SiteContents.ToList();
            model.AssociateMembershipText = contents.Where(c => c.ContentType == "AssociateMembershipText").Select(c => c.Content).First();
            model.FullMembershipText = contents.Where(c => c.ContentType == "FullMembershipText").Select(c => c.Content).First();

            return View(model);
        }
        
        public ActionResult ConfirmMembership(string membershipType, string membershipExpired)
        {
            ViewBag.MembershipType = 0;
            ViewBag.MembershipExpired = false;

            //  20170318 SA Return the view according to whether the user had a previous membership or not.
            if (!string.IsNullOrWhiteSpace(membershipType))
            {
                ViewBag.MembershipType = Convert.ToInt32(membershipType);
            }

            if (!string.IsNullOrWhiteSpace(membershipExpired))
            {
                ViewBag.MembershipExpired = Convert.ToBoolean(membershipExpired);
            }
            return View();
        }

        
        public ActionResult Details(string id)
        {

            //20170319 SA Redirect the user to the login page if they are not logged in
            if (string.IsNullOrWhiteSpace(Utilities.GetLoggedInUserId(User)))
            {
                return Redirect("/account/login");
            }

            if ((Utilities.GetLoggedInUserId(User)).ToLower() == id.ToLower())
            {
                //20170328 SA If this is the current user, then we don't need to stop them
            }
            else if (!Utilities.IsUserAdmin(User) && !Utilities.IsMembershipValid(Utilities.GetMember(User)))
            {
                // 20170328 SA If the user is not an admin user and they are not a valid member, then take the to the page to buy membership from.
                return Redirect("/members/refer/");
            }

            AspNetUser aspNetUser = db.AspNetUsers.Find(id);
            if (aspNetUser == null)
            {
                return HttpNotFound();
            }

            // 20170318 SA Getting the viewBag ready to display the membership information to the member on the page.
            ViewBag.MembershipInfo = "Non-member";

            if (aspNetUser.MemberType.HasValue)
            {
                if (!aspNetUser.LastRenewalDate.HasValue)
                {
                    // 20170406 SA Keep as a non-member because they never renewed.
                }
                else if (aspNetUser.MemberType == 2)
                {
                    ViewBag.MembershipInfo = "Associate membership expiry: " + aspNetUser.LastRenewalDate.Value.AddDays(365).ToString("yyyy-MM-dd");
                }
                else if (aspNetUser.MemberType == 3)
                {
                    ViewBag.MembershipInfo = "Full membership expiry: " + aspNetUser.LastRenewalDate.Value.AddDays(365).ToString("yyyy-MM-dd");
                }
            }

            // 20170318 SA Populates the message that lets the admin know if this user has been made an admin or removed.
            ViewBag.PermissionChanged = TempData["Message"];
            ViewBag.IsMemberAdmin = UserManager.GetRoles(id).Contains("Admin");

            return View(aspNetUser);
        }

        // 20170318 SA Used to highlight the current menu item in the layout page.
        public void SetViewBagCurrentMenu()
        {
            ViewBag.CurrentMenu = "members";
            ViewBag.Title = "Members | Million Bokeh Photographer's Association";
            if (!string.IsNullOrWhiteSpace(ViewBag.FindPhotographer) && Convert.ToBoolean(ViewBag.FindPhotographer))
            {
                ViewBag.CurrentMenu = "findphotographer";
                ViewBag.Title = "Find A Photographer | Million Bokeh Photographer's Association";
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult CreateAdminUser()
        {
            // 20170328 SA Pass the "IsRegisteringAdmin" to the register page, so that it can display only the fields associated with registering a admin.
            TempData["IsRegisteringAdmin"] = true;
            return Redirect("/account/register");
        }


        // 20170319 SA Used to edit the member details fields
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string Edit(string id, string propertyName, string propertyvalue)
        {
            var memberToUpdate = db.AspNetUsers.Find(id);
            System.Reflection.PropertyInfo prop = typeof(AspNetUser).GetProperty(propertyName);
            if (prop.PropertyType == typeof(string))
            {
                prop.SetValue(memberToUpdate, propertyvalue);
            }
            if (prop.PropertyType == typeof(int?))
            {
                // 20170330 SA Only one type of int? exists (the photography rangeID), so we can return a custom message to replace the html with
                if (string.IsNullOrWhiteSpace(propertyvalue))
                {
                    prop.SetValue(memberToUpdate, null);
                    propertyvalue = "Not available for commercial photography.";
                }
                else
                { 
                    prop.SetValue(memberToUpdate, Convert.ToInt32(propertyvalue));
                    Ranx r = db.Ranges.Find(memberToUpdate.RangeId);
                    propertyvalue = r.Name; // 20170330 SA return the name of the new range, rather than the id, because we need to show that value to the user
                }
            }
            db.SaveChanges();
            return propertyvalue;
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
            var results = db.AspNetUsers.Where(n => n.Name.Contains(term)).Select(n => n.Name).Take(5).ToList();
            return JsonConvert.SerializeObject(results);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string UploadImage(string id)
        {
            
            var fileName = "";
            if (System.Web.HttpContext.Current.Request.Files.AllKeys.Any())
            {
                var pic = System.Web.HttpContext.Current.Request.Files["Image"];

                // 20170326 SA Create a new filename with the id and the file extension to avoid conflicting file names in the folder
                fileName = Path.GetFileName(id + Path.GetExtension(pic.FileName));
                var path = Path.Combine(Server.MapPath("~/images/MemberPhotos/profile"), fileName);
                pic.SaveAs(path);

                // 20170326 SA Make the image a square.
                Utilities.ProcessImage(path, path, 400, 400);

                var toUpdate = db.AspNetUsers.Find(id);
                toUpdate.Image = fileName;
                db.SaveChanges();

            }
            return fileName;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string UploadGalleryImage(string id)
        {
            var fileName = "";
            if (System.Web.HttpContext.Current.Request.Files.AllKeys.Any())
            {
                Guid picId = Guid.NewGuid();
                var pic = System.Web.HttpContext.Current.Request.Files["Image"];

                //  20170326 SA Create a new filename with the id and the file extension to avoid conflicting file names in the folder.
                fileName = Path.GetFileName(picId + Path.GetExtension(pic.FileName));
                var path = Path.Combine(Server.MapPath("~/images/MemberPhotos/gallery/full"), fileName);
                pic.SaveAs(path);

                //  20170326 SA Get the path where the thumbnail will be saved.
                var thumbnailpath = Path.Combine(Server.MapPath("~/images/MemberPhotos/gallery/thumbnail"), fileName);

                //  20170326 SA Make the thumbnail image the right size for the gallery.
                Utilities.ProcessImage(path, thumbnailpath, 300, 400);

                Photo newPhoto = new Photo()
                {
                    UserId = id,
                    PhotoPath = fileName,
                    PhotoId = picId
                };

                db.Photos.Add(newPhoto);
                db.SaveChanges();
            }
            return fileName;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string DeleteGalleryImage(Guid id)
        {
            Photo toDelete = db.Photos.Find(id);

            var path = Path.Combine(Server.MapPath("~/images/MemberPhotos/gallery/full"), toDelete.PhotoPath);
            var thumbnailpath = Path.Combine(Server.MapPath("~/images/MemberPhotos/gallery/thumbnail"), toDelete.PhotoPath);
            new FileInfo(path).Delete();
            new FileInfo(thumbnailpath).Delete();

            db.Photos.Remove((toDelete));
            db.SaveChanges();
            return id.ToString();
        }

        
    }
}
