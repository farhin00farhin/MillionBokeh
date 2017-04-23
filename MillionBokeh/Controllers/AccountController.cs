using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using MillionBokeh.Models;
using Microsoft.AspNet.Identity;

namespace MillionBokeh.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        #region Managers
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationRoleManager _roleManager;

        private MillionBokehEntities db = new MillionBokehEntities();

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

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

        public ApplicationRoleManager RoleManager
        {
            get
            {
                return _roleManager ?? HttpContext.GetOwinContext().Get<ApplicationRoleManager>();
            }
            private set
            {
                _roleManager = value;
            }
        } 
        #endregion

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.IsRegisteringAdmin = false;
            ViewBag.Login = true;
            ViewBag.ReturnUrl = returnUrl;
            return View(new AuthenticationInfoViewModel() { Locations = db.Locations.ToList() } );
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(AuthenticationInfoViewModel model, string returnUrl)
        {
            ViewBag.Login = true;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = SignInManager.PasswordSignIn(model.Email, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    ViewBag.MembershipExpired = false;
                    var member = GetMemberByEmail(model.Email);
                    if (member.MemberType > 1)
                    {
                        if (!Utilities.IsMembershipValid(member))
                        {
                            return 
                                Redirect(string.Format("/members/ConfirmMembership?membershipType={0}&membershipExpired=true", member.MemberType));
                        }
                    }
                    return Redirect("/");
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    ViewBag.LoginError = true;
                    return View(model);
            }
        }

        private AspNetUser GetMemberByEmail(string email)
        {
            return db.AspNetUsers.Where(u => u.Email.ToLower() == email.ToLower()).First();
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            // This page is used to register both a normal user and an admin, so to distinguish between the two, we
            // would have been passed a Tempdata from somewhere else. Use this to display the appropriate sections
            // on the page
            ViewBag.IsRegisteringAdmin = false;
            if (TempData["IsRegisteringAdmin"] != null && Convert.ToBoolean(TempData["IsRegisteringAdmin"]))
            {
                ViewBag.IsRegisteringAdmin = true;
            }
            ViewBag.Login = false;

            return View("Login", new AuthenticationInfoViewModel() { Locations = db.Locations.ToList()});
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(AuthenticationInfoViewModel model, int[] regions)
        {
            if (ModelState.IsValid)
            {
                string newUserId = Guid.NewGuid().ToString();
                var user = new ApplicationUser() {Id = newUserId, UserName = model.Email, Email = model.Email, Name = (model.FirstName + " " + model.LastName), PhoneNumber = model.PhoneNumber, Description = model.Profile, RangeId = model.RangeId, MemberType = model.MembershipType, Image = "No_image_available.png" };
                try
                {
                    var result = UserManager.Create(user, model.Password);
                    if (result.Succeeded)
                    {
                        if (regions != null)
                        {
                            foreach (var regionId in regions)
                            {
                                db.UserLocations.Add(new UserLocation() {LocationId = regionId, UserId = newUserId});
                            }
                            db.SaveChanges();
                        }

                        SignInManager.SignIn(user, isPersistent: false, rememberBrowser: false);

                        // upload the image
                        UploadMemberImage(newUserId);

                        if (model.MembershipType > 1)
                        {
                            return Redirect("/members/confirmmembership?membershipType=" + model.MembershipType);
                        }
                        else
                        {
                            return Redirect("/members/details?id=" + newUserId);
                        }
                    }
                    AddErrors(result);

                }
                catch (Exception ex)
                {

                    throw ex;
                }
            }

            model.Locations = db.Locations;
            return View("Login", model);
        }

        private void UploadMemberImage(string newUserId)
        {
            string fileName = "No_image_available.png";

            if (System.Web.HttpContext.Current.Request.Files.AllKeys.Any())
            {
                var pic = System.Web.HttpContext.Current.Request.Files["ImageAttachment"];
                if (pic != null && !string.IsNullOrWhiteSpace(pic.FileName))
                {
                    fileName = Path.GetFileName(newUserId + Path.GetExtension(pic.FileName));
                    var path = Path.Combine(Server.MapPath("~/images/MemberPhotos/profile"), fileName);
                    pic.SaveAs(path);
                    Utilities.ProcessImage(path, path, 400, 400);
                }
            }

            var toUpdate = db.AspNetUsers.Find(newUserId);
            if (toUpdate != null)
            {
                toUpdate.Image = fileName;
            }
            db.SaveChanges();
        }


        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterAdmin(AuthenticationInfoViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser() { UserName = model.Email, Email = model.Email, MemberType = 0 };
                try
                {
                    var result = await UserManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                        var role = await RoleManager.FindByNameAsync("Admin");
                        var roleResult = await UserManager.AddToRolesAsync(user.Id, "Admin");
                        if (!roleResult.Succeeded)
                        {
                            ModelState.AddModelError("", result.Errors.First());
                        }

                        AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                        return Redirect("/account/login");

                    }
                    AddErrors(result);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            // If we got this far, something failed, redisplay form
            ViewBag.IsRegisteringAdmin = true;
            ViewBag.Login = false;
            return View("Login", model);
        }


        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> MakeAdminUser(string userid)
        {
            var roleResult = await UserManager.AddToRolesAsync(userid, "Admin");
            if (roleResult.Succeeded)
            {
                TempData["Message"] = "This user is now an admin";
            }
            return Redirect("/members/details?id=" + userid);
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveAdminUser(string userid)
        {
            var roleResult = await UserManager.RemoveFromRoleAsync(userid, "Admin");
            if (roleResult.Succeeded)
            {
                TempData["Message"] = "This user is no longer an admin";
            }
            return Redirect("/members/details?id=" + userid);
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            var f = new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
            return f;
        }

     
        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public ActionResult ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = AuthenticationManager.GetExternalLoginInfo();
            if (loginInfo == null)
            {
                return RedirectToAction("Login", new AuthenticationInfoViewModel() { Locations = db.Locations.ToList() });
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = SignInManager.ExternalSignIn(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });
                case SignInStatus.Failure:
                default:
                    // If the user does not have an account, then prompt the user to create an account
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                    return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, PhoneNumber = "----------", Image = "No_image_available.png" };
                var result = await UserManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await UserManager.AddLoginAsync(user.Id, info.Login);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                if (!error.StartsWith("Name "))
                {
                    ModelState.AddModelError("", error);
                }
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}