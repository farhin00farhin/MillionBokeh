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

namespace MillionBokeh.Controllers
{
    public class ProductsController : Controller
    {
        private MillionBokehEntities db = new MillionBokehEntities();

        public ActionResult Index(string category, string term, string pagenumber, string sortby, string sortorder)
        {
            //20170317 SA  Add the current state to the viewbag to keep scope in the view.
            ViewBag.CurrentMenu = "products";
            ViewBag.Category = category;
            ViewBag.Term = term;
            ViewBag.SortBy = sortby;
            ViewBag.SortOrder = sortorder;
            ViewBag.CurrentPage = pagenumber;

            //20170317 SA Filter using LINQ step by step to return products according to the scope
            IEnumerable<Product> productsToReturn = db.Products;
            productsToReturn = ApplySort(productsToReturn);
            productsToReturn = FilterByCategory(productsToReturn);
            productsToReturn = FilterByTerm(productsToReturn);
            productsToReturn = GetOnlyCurrentPage(productsToReturn);
            productsToReturn = PopulateRatings(productsToReturn);

            // 20170317 SA  Help the page to display the correct links
            GeneratePageUrls();

            return View(productsToReturn.ToList());
        }

        #region Filtering and sorting

        private IEnumerable<Product> FilterByCategory(IEnumerable<Product> fullProductList)
        {
            if (!string.IsNullOrWhiteSpace(ViewBag.Category))
            {
                int categoryInt = Convert.ToInt32(ViewBag.Category);
                if (categoryInt > 0)
                {
                    ViewBag.Category = categoryInt;
                    return fullProductList.Where(e => e.CategoryID == categoryInt);
                }
            }

            return fullProductList;
        }

        private IEnumerable<Product> FilterByTerm(IEnumerable<Product> fullProductList)
        {
            if (!string.IsNullOrWhiteSpace(ViewBag.Term))
            {
                string term = Convert.ToString(ViewBag.Term);
                return fullProductList.Where(e => e.ProductName.Contains(term) || e.Description.Contains(term));
            }

            return fullProductList;
        }

        private IEnumerable<Product> GetOnlyCurrentPage(IEnumerable<Product> fullProductList)
        {
            ViewBag.TotalNum = fullProductList.Count();
            UpdatePageNumbers();
            int productsPerPage = 9;
            int pageNumber = ViewBag.CurrentPage;
            return fullProductList.Skip((pageNumber - 1) * productsPerPage).Take(productsPerPage);
        }
        private IEnumerable<Product> PopulateRatings(IEnumerable<Product> fullProductList)
        {
            fullProductList.ToList().ForEach(i => i.Rating = GetProductStars(i));
            return fullProductList;
        }

        private IEnumerable<Product> ApplySort(IEnumerable<Product> fullProductList)
        {
            //20170317 SA default sorting parameters 
            if (string.IsNullOrWhiteSpace(ViewBag.SortBy))
            {
                ViewBag.SortBy = "name";
            }
            if (string.IsNullOrWhiteSpace(ViewBag.SortOrder))
            {
                ViewBag.SortOrder = "asc";
            }

            //20170317 SA override default sorting parameters if needed
            if (ViewBag.SortOrder == "desc")
            {
                if (ViewBag.SortBy == "price")
                {
                    fullProductList = fullProductList.OrderByDescending(p => p.UnitPrice);
                }
                else
                {
                    fullProductList = fullProductList.OrderByDescending(p => p.ProductName);
                }
            }
            else
            {
                if (ViewBag.SortBy == "price")
                {
                    fullProductList = fullProductList.OrderBy(p => p.UnitPrice);
                }
                else
                {
                    fullProductList = fullProductList.OrderBy(p => p.ProductName);
                }
            }

            //20170317 SA Change the text in the dropdown according to the type that the user wants to sort by
            ViewBag.AscText = ViewBag.SortBy == "price" ? "Low to High" : "A to Z";
            ViewBag.DescText = ViewBag.SortBy == "price" ? "High to Low" : "Z to A";


            return fullProductList;
        }

        #endregion


        #region Display helper methods

        private void UpdatePageNumbers()
        {
            int pageNumber = 1;
            if (Request.Params["PageNumber"] != null && !string.IsNullOrWhiteSpace(Request.Params["PageNumber"]))
            {
                pageNumber = Convert.ToInt32(Request.Params["PageNumber"]);
            }
            ViewBag.CurrentPage = pageNumber;
            ViewBag.NextPage = pageNumber >= (Math.Ceiling((double)(ViewBag.TotalNum / 9.0))) ? pageNumber : pageNumber + 1;
            ViewBag.PreviousPage = pageNumber <= 1 ? 1 : pageNumber - 1;

            ViewBag.PreviousPageDisabled = ViewBag.CurrentPage == ViewBag.PreviousPage ? " disabled fade" : "";
            ViewBag.NextPageDisabled = ViewBag.CurrentPage == ViewBag.NextPage ? "disabled fade" : "";
        }

        // 20170318 SA Generate the page number URL format based on the current ViewBag State. This ensures that the state of the page will be kept in every button on the pager.
        private void GeneratePageUrls()
        {
            ViewBag.Category = 0;
            if (Request.Params["category"] != null && !string.IsNullOrWhiteSpace(Request.Params["category"]))
            {
                ViewBag.Category = Convert.ToInt32(Request.Params["category"]);
            }

            if (Request.Params["term"] != null && !string.IsNullOrWhiteSpace(Request.Params["term"]))
            {
                ViewBag.Term = Convert.ToString(Request.Params["term"]);
            }

            ViewBag.PageNumberUrl = string.Format("?category={0}&term={1}&PageNumber={2}&SortBy={3}&SortOrder={4}", ViewBag.Category, ViewBag.Term, "{0}", ViewBag.SortBy, ViewBag.SortOrder);

        } 
        #endregion


        private short GetProductStars(Product p)
        {
            var items = db.Reviews.Where(i => i.ProductId == p.ProductID).ToList();
            int totalstars = items.Sum(i => i.StarRating);
            if (totalstars == 0)
            {
                //20170326 SA by default, return 3 stars because we don't want new products to look too bad.
                return 3;
            }
            return Convert.ToInt16(Math.Round(totalstars/(double)items.Count(), 0));
        }

        public ActionResult Details(int? id)
        {
            ViewBag.CurrentMenu = "products";
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            product.Rating = GetProductStars(product);

            // 20170411 SA Return the most available products along with the product being displayed so that they have a chance of selling fast.
            product.Related = db.Products.Where(x => x.CategoryID == product.CategoryID).OrderByDescending(x => x.AvailableUnits).Take(4).ToList();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create()
        {
            //  20170326 SA new products will have these by default. The admin will change them straight away.
            Product newProduct = new Product()
            {
                CategoryID = 1,
                Description = "Enter product description here",
                Image = "No_image_available.png",
                ProductInfo = "Enter Product Information here",
                ProductName = "New Product",
                Rating = 3,
                Subtitle = "Enter product subtitle here",
                UnitPrice = 0.00M,
                AvailableUnits = 10
            };

            db.Products.Add((newProduct));
            db.SaveChanges();

            return Redirect("/products/details?id=" + newProduct.ProductID);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public string Edit(int id, string propertyName, string propertyvalue)
        {
            var productToUpdate = db.Products.Find(id);
            System.Reflection.PropertyInfo prop = typeof(Product).GetProperty(propertyName);
            if (prop.PropertyType == typeof(string))
            {
                prop.SetValue(productToUpdate, propertyvalue);

            }
            else if (prop.PropertyType == typeof(decimal))
            {
                prop.SetValue(productToUpdate, Convert.ToDecimal(propertyvalue));
            }
            else if (prop.PropertyType == typeof(int))
            {
                prop.SetValue(productToUpdate, Convert.ToInt32(propertyvalue));
                if (prop.Name == "CategoryID")
                {
                    Category c = db.Categories.Find(productToUpdate.CategoryID);
                    propertyvalue = c.Name;
                        // 20170330 SA return the name of the new category, rather than the id, because we need to show that value to the user
                }
            }
            db.SaveChanges();
            return propertyvalue;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            Product product = db.Products.Find(id);
            db.Products.Remove(product);
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

        [HttpPost]
        public string AutoComplete(string term)
        {
            var results = db.Products.Where(n => n.ProductName.Contains(term)).Take(5).Select(n => n.ProductName).ToList();
            return JsonConvert.SerializeObject(results);
        }

        public string UploadImage(int id)
        {
            var fileName = "";
            if (System.Web.HttpContext.Current.Request.Files.AllKeys.Any())
            {
                var pic = System.Web.HttpContext.Current.Request.Files["Image"];
                fileName = Path.GetFileName(id + Path.GetExtension(pic.FileName));
                var path = Path.Combine(Server.MapPath("~/images/products/"), fileName);
                pic.SaveAs(path);

                var productToUpdate = db.Products.Find(id);
                productToUpdate.Image = fileName;
                db.SaveChanges();

            }
            return fileName;
        }
    }
}
