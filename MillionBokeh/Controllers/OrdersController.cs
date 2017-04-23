using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using MillionBokeh.Models;
using PayPal.Api;
using Newtonsoft.Json;

namespace MillionBokeh.Controllers
{
    public class OrdersController : Controller
    {
        private MillionBokehEntities db = new MillionBokehEntities();

        public ActionResult Index()
        {
            ViewBag.Total = 0;
            ViewBag.DiscountTotal = 0;

            string currentUserId = Utilities.GetLoggedInUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Redirect("/account/login");
            }

            Models.Order order = GetCurrentOrderWithItems();
            if (order == null)
            {
                return HttpNotFound();
            }

            // 20170318 SA  Populate the total with the sum of the orderitem prices then apply the discount
            ViewBag.Total = order.OrderItems.Sum(o => o.Quantity * o.ItemPrice);
            ViewBag.DiscountedPrice = GetDiscountedTotal(ViewBag.Total);

            return View(order);
        }

        // 20170318 SA  Get the current order and populates the orderitems collection so that the price and description information of
        // all types of items are loaded and ready to display
        private Models.Order GetCurrentOrderWithItems()
        {
            Models.Order order = GetCurrentOrderForLoggedInUser();
            if (order == null)
            {
                return null;
            }

            foreach (var item in order.OrderItems)
            {
                switch (item.ItemType)
                {
                    case "product":
                        var product = db.Products.Where(p => p.ProductID == item.ItemId).First();
                        item.ItemName = product.ProductName;
                        item.ItemDescription = product.Description.Substring(0, (product.Description.Length > 200 ? 200 : product.Description.Length));
                        item.ItemPrice = product.UnitPrice;
                        item.ItemImage = product.Image;
                        item.ItemsAvailable = product.AvailableUnits;
                        break;
                    case "event":
                        var eve = db.Events.Where(e => e.EventID == item.ItemId).First();
                        item.ItemName = eve.Name;
                        item.ItemDescription = eve.Description.Substring(0, (eve.Description.Length > 200 ? 200 : eve.Description.Length));
                        item.ItemPrice = eve.Fee;
                        item.ItemImage = eve.Image;
                        item.ItemsAvailable = 100; // 20170411 SA Make this a high number to allow people to buy as many event tickets as they want.
                        break;
                    default:
                        break;
                }
            }

            return order;
        }


        //  20170319 SA This is the action that gets called when a user clicks on the Buy button from the events or product detail pages.
        public ActionResult Add(int itemId, string itemType)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("/account/login");
            }

            Models.Order addingItemTo = GetCurrentOrderForLoggedInUser();

            if (addingItemTo.OrderItems.Count(o => o.ItemId == itemId && o.ItemType == itemType) > 0)
            {
                OrderItem toUpdate = addingItemTo.OrderItems.Where(o => o.ItemId == itemId && o.ItemType == itemType).First();
                toUpdate.Quantity++;
            }
            else
            {
                OrderItem newItem = new OrderItem() { ItemId = itemId, ItemType = itemType, OrderId = addingItemTo.Id, Quantity = 1 };
                db.OrderItems.Add(newItem);
            }

            db.SaveChanges();

            return Redirect("/orders/index");
        }

        //  20170319 SA Delete an item from the cart (however many quantities)
        public ActionResult DeleteItem(int itemId, string itemType)
        {
            Models.Order deletingFrom = GetCurrentOrderForLoggedInUser();
            OrderItem toDelete = deletingFrom.OrderItems.Where(o => o.ItemId == itemId && o.ItemType == itemType).First();
            db.OrderItems.Remove(toDelete);
            db.SaveChanges();
            return Redirect("/orders/index");
        }


        //  20170319 SA Increase/decrease the item's qunatity in the order
        public string UpdateItem(int itemId, string itemType, short quantity)
        {
            if (itemType == "product")
            {
                var productToBuy = db.Products.First(o => o.ProductID == itemId);
                if ((productToBuy.AvailableUnits - quantity) < 0)
                {
                    return JsonConvert.SerializeObject(new { error = "There are no more available in stock" });
                }
            }

            Models.Order updatingFrom = GetCurrentOrderForLoggedInUser();
            OrderItem toUpdate = updatingFrom.OrderItems.Where(o => o.ItemId == itemId && o.ItemType == itemType).First();
            toUpdate.Quantity = quantity;
            db.SaveChanges();

            Models.Order order = GetCurrentOrderWithItems();

            // 20170319 SA return the subtotal as a string
            decimal subTotal = order.OrderItems.Where(o => o.ItemId == itemId && o.ItemType == itemType).Sum(o => o.ItemPrice*o.Quantity);
            decimal total = order.OrderItems.Sum(o => o.ItemPrice * o.Quantity);
            decimal discountedTotal = GetDiscountedTotal(total);

            return JsonConvert.SerializeObject(new { subTotal = subTotal.ToString("0.00"), total = total.ToString("0.00"), discountedTotal = discountedTotal.ToString("0.00") });
        }

        private decimal GetDiscountedTotal(decimal total)
        {
            // 20170411 SA recalculate discounted price
            decimal discountedTotal = total;
            AspNetUser member = Utilities.GetMember(User);
            if (member.MemberType == 2 && Utilities.IsMembershipValid(member))
            {
                discountedTotal = total*0.92M;
            }
            else if (member.MemberType == 3 && Utilities.IsMembershipValid(member))
            {
                discountedTotal = total*0.85M;
            }
            return discountedTotal;
        }

        // 20170318 SA Returns an order or makes a new order if one doesn't exist for the user. Only one order is possible for a user at any time
        private Models.Order GetCurrentOrderForLoggedInUser()
        {
            string currentUserId = Utilities.GetLoggedInUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return null;
            }

            var orders = db.Orders.Include(o => o.AspNetUser).Where(o => o.CustomerId == currentUserId && o.Status == 0);
            if (orders.Count() > 0)
            {
                // 20170318 SA return existing order
                return orders.First();
            }
            else
            {
                try
                {
                    Models.Order newOrder = new Models.Order() { CustomerId = Utilities.GetLoggedInUserId(User), OrderDate = DateTime.Now, Status = 0 };
                    db.Orders.Add(newOrder);
                    db.SaveChanges();
                    return newOrder;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

        }

        //  20170318 SA Displays the payment page to get all the credit card details from the user.
        public ActionResult Payment(string membershipOption)
        {
            string currentUserId = Utilities.GetLoggedInUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Redirect("/account/login");
            }

            // 20170318 SA Member types
            // 2 = Associate member
            // 3 = Full member

            ViewBag.Total = 0;
            ViewBag.MembershipOption = 0;
            if (membershipOption == "3")
            {
                ViewBag.Total = "300";
                ViewBag.OrderName = "Full Membership";
                ViewBag.MembershipOption = 3;
            }
            else if (membershipOption == "2")
            {
                ViewBag.Total = "180";
                ViewBag.OrderName = "Associate Membership";
                ViewBag.MembershipOption = 2;
            }
            else
            {
                Models.Order currentOrder = GetCurrentOrderForLoggedInUser();
                currentOrder = PopulateOrderPrices(currentOrder);
                var member = Utilities.GetMember(User);
                if (member == null)
                {
                    return Redirect("/account/login");
                }
                if (member.MemberType == 2 && Utilities.IsMembershipValid(member))
                {
                    decimal total = currentOrder.OrderItems.Sum(oi => oi.ItemPrice * oi.Quantity);
                    ViewBag.Total = total * 0.92M;

                }
                else if (member.MemberType == 3 && Utilities.IsMembershipValid(member))
                {
                    decimal total = currentOrder.OrderItems.Sum(oi => oi.ItemPrice * oi.Quantity);
                    ViewBag.Total = total * 0.85M;
                }
                else
                {
                    ViewBag.Total = currentOrder.OrderItems.Sum(oi => oi.ItemPrice * oi.Quantity);
                }

                ViewBag.OrderName = "Shopping Cart";
                ViewBag.OrderNumber = currentOrder.Id;
            }
            ViewBag.Title = "Checkout | Million Bokeh Photographer's Association";

            #if DEBUG
                ViewBag.Debugging = true;
            #endif
            return View();
        }



        //  20170318 SA Hydrates the extra porperties of the partial class with the name, description and price for each item, regardless of type.
        private Models.Order PopulateOrderPrices(Models.Order order)
        {
            foreach (var item in order.OrderItems)
            {
                switch (item.ItemType)
                {
                    case "product":
                        var product = db.Products.Where(p => p.ProductID == item.ItemId).First();
                        item.ItemName = product.ProductName;
                        item.ItemDescription = product.Description.Substring(0, (product.Description.Length > 200 ? 200 : product.Description.Length));
                        item.ItemPrice = product.UnitPrice;
                        break;
                    case "event":
                        var eve = db.Events.Where(e => e.EventID == item.ItemId).First();
                        item.ItemName = eve.Name;
                        item.ItemDescription = eve.Description.Substring(0, (eve.Description.Length > 200 ? 200 : eve.Description.Length));
                        item.ItemPrice = eve.Fee;
                        break;
                    default:
                        break;
                }
            }
            return order;
        }

        //  20170318 SA This uses the Paypal REST API to verify a credit card and make a payment
        [HttpPost]
        public ActionResult Payment(string orderName, string totalPrice, string city, string country, string line1, string postCode, string cvv2, int expire_month, int expire_year, string firstname, string lastname, string cardnumber, string type, string orderNumber, short membershipType)
        {
            Payment createdPayment = null;
            string currencyCode = "USD";

            Item item = new Item();
            item.name = orderName;
            item.currency = currencyCode;
            item.price = Convert.ToDouble(totalPrice).ToString("0.00");
            item.quantity = "1";
            item.sku = "00000";

            List<Item> itms = new List<Item>();
            itms.Add(item);
            ItemList itemList = new ItemList();
            itemList.items = itms;

            Address billingAddress = new Address();
            billingAddress.city = city;
            billingAddress.country_code = country;
            billingAddress.line1 = line1;
            billingAddress.postal_code = postCode;

            CreditCard crdtCard = new CreditCard();
            crdtCard.billing_address = billingAddress;
            crdtCard.cvv2 = cvv2;  //card cvv2 number
            crdtCard.expire_month = 1; //card expire date
            crdtCard.expire_year = expire_year; //card expire year
            crdtCard.first_name = firstname;
            crdtCard.last_name = lastname;
            crdtCard.number = cardnumber; //credit card number
            crdtCard.type = type; //credit card type

            Details details = new Details();
            details.shipping = "0";
            details.subtotal = Convert.ToDouble(totalPrice).ToString("0.00");
            details.tax = "0";

            Amount amnt = new Amount();
            amnt.currency = currencyCode;
            amnt.total = Convert.ToDouble(totalPrice).ToString("0.00");
            amnt.details = details;

            Transaction tran = new Transaction();
            tran.amount = amnt;
            tran.description = orderName;
            tran.item_list = itemList;
            tran.invoice_number = orderNumber;

            List<Transaction> transactions = new List<Transaction>();
            transactions.Add(tran);

            FundingInstrument fundInstrument = new FundingInstrument();
            fundInstrument.credit_card = crdtCard;

            List<FundingInstrument> fundingInstrumentList = new List<FundingInstrument>();
            fundingInstrumentList.Add(fundInstrument);

            Payer payr = new Payer();
            payr.funding_instruments = fundingInstrumentList;
            payr.payment_method = "credit_card";

            Payment pymnt = new Payment();
            pymnt.intent = "sale";
            pymnt.payer = payr;
            pymnt.transactions = transactions;

            // 20170413 SA I had to add this in incase PayPal stops working. This should only return true on a test environment based on the "disablePaypal" web.config setting.
            if (!Configuration.IsPaypalDisabled())
            {
                try
                {
                    APIContext apiContext = Configuration.GetAPIContext();
                    createdPayment = pymnt.Create(apiContext);
                    if (createdPayment.state.ToLower() != "approved")
                    {
                        return View("Failure");
                    }
                }
                catch (PayPal.PayPalException ex)
                {
                    LogOrderError(((PayPal.ConnectionException)ex).Response);
                    return View("Failure");
                }
            }

            IEnumerable<OrderItem> items = null;

            if (membershipType > 1)
            {
                // 20170318 SA it was a membership that they paid for, so update the membership details
                UpdateMembership(membershipType);
                return Redirect("/members/details?id=" + Utilities.GetLoggedInUserId(User));
                //return Redirect("/");
            }
            else
            {
                // 20170318 SA must have been an order that they paid for, so update the order details
                Models.Order current = GetCurrentOrderWithItems();
                UpdateOrderStatus(Convert.ToDecimal(totalPrice));
                items = current.OrderItems.Where(i => i.ItemType == "product");

                return View("ReviewPurchase", items);
            }

        }



        //  20170318 SA After a successful payment of the membership fee, we update the aspnetuser table
        private void UpdateMembership(short membershipType)
        {
            string userId = Utilities.GetLoggedInUserId(User);
            var currentUser = db.AspNetUsers.Find(userId);
            currentUser.MemberType = membershipType;
            currentUser.LastRenewalDate = DateTime.Now;
            db.SaveChanges();
        }

        //  20170318 SA After a successful payment of the order total, we update the order table
        private void UpdateOrderStatus(decimal totalPrice)
        {
            UpdateProductStatus();
            string userId = Utilities.GetLoggedInUserId(User);
            var orders = db.Orders.Where(o => o.CustomerId == userId && o.Status == 0);
            if (orders.Count() > 0)
            {
                //Get the order
                Models.Order orderToUpdate = orders.First();
                orderToUpdate.PaymentTotal = totalPrice;
                orderToUpdate.PaymentDateTime = DateTime.Now;
                orderToUpdate.Status = 1;
                db.SaveChanges();
            }
        }


        // 20170411 SA After a successful payment of the order total, we update the order table
        private void UpdateProductStatus()
        {
            string userId = Utilities.GetLoggedInUserId(User);
            var orders = db.Orders.Where(o => o.CustomerId == userId && o.Status == 0);
            if (orders.Count() > 0)
            {
                // 20170411 SA Get the order
                Models.Order orderToUpdate = orders.First();

                // 20170411 SA Get the products in the order and subtract the quantities from the availability
                var items = db.OrderItems.Where(o => o.ItemType == "product" && o.OrderId == orderToUpdate.Id).ToList();
                foreach (var item in items)
                {
                    var productToUpdate = db.Products.First(p => p.ProductID == item.ItemId);
                    productToUpdate.AvailableUnits -= item.Quantity;
                    db.SaveChanges();
                }
            }
        }

        //  20170318 SA Record the error in the order table for a system admin to look at later
        private void LogOrderError(string error)
        {
            string userId = Utilities.GetLoggedInUserId(User);
            var orders = db.Orders.Where(o => o.CustomerId == userId && o.Status == 0);
            if (orders.Count() > 0)
            {
                //Get the order
                Models.Order orderToUpdate = orders.First();
                orderToUpdate.ErrorMessage  = error;
                db.SaveChanges();
            }
        }


        //  20170326 SA Goes through each item in the form and saves the review in the review table
        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult SubmitReview(FormCollection formResult)
        {
            string userId = Utilities.GetLoggedInUserId(User);
            for (int i = 1; i < formResult.AllKeys.Count(); i = i + 3)
            {
                Review newReview = new Review()
                {
                    MemberId = userId,
                    Date = DateTime.Now,
                    Description = formResult[i + 1],
                    ProductId = Convert.ToInt32(formResult[i]),
                    StarRating = Convert.ToInt16(formResult[i + 2])
                };

                db.Reviews.Add(newReview);
            }

            db.SaveChanges();
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
