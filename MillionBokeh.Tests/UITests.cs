using System;
using System.Configuration;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MillionBokeh.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;


namespace MillionBokeh.Tests
{
    [TestClass]
    public class UITests
    {

        private static string HomeUrl = ConfigurationManager.AppSettings["homeurl"];

        [TestMethod]
        public void TestLoginAndBuyProduct()
        {
            var driver = InitialiseWebDriver();

            // 20170402 SA Login to test as a normal member
            Login(driver, "AllanSStarrett@dayrep.com", "Password99!");

            // 20170402 SA Add a product to the cart and check out
            AddProductToCart(driver, "Directional stereo Microphone XM0-E1");
            CheckoutInShoppingCart(driver);

            // 20170402 SA Fill up the credit card form
            FillCreditCardForm(driver);

            // 20170402 SA After a successful purchase, leave a review and submit
            LeaveReviews(driver);

            Assert.IsTrue(driver.PageSource.Contains("Thank you"));

            driver.Quit();
        }

        [TestMethod]
        public void TestSignupAndBuyEvent()
        {
            var driver = InitialiseWebDriver();

            // 20170402 SA Sign up a new member
            string newEmail = "test" + Guid.NewGuid().ToString("N") + "@testtest.com";
            SignUpUser(driver, newEmail, "Password99!", "none");

            // 20170402 SA Add a event ticket to the cart and check out
            AddEventToCartAndCheckout(driver, "ISO, Aperture and Shutter Speed");

            // 20170402 SA Fill up the credit card form
            FillCreditCardForm(driver);

            Assert.IsTrue(driver.PageSource.Contains("Congratulations"));
            driver.Quit();

        }

        [TestMethod]
        public void TestSignupAndChangeImage()
        {
            var driver = InitialiseWebDriver();

            string newEmail = "test" + Guid.NewGuid().ToString("N") + "@testtest.com";
            SignUpUser(driver, newEmail, "Password99!", "none");

            var element = driver.FindElement(By.LinkText(newEmail));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            element = driver.FindElement(By.CssSelector("#member-image"));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            element = driver.FindElement(By.CssSelector("input[data-property='Image']"));
            element.SendKeys(@"D:\Code\MillionBokeh\MillionBokeh\images\MemberPhotos\profile\boy-801182_1920.jpg");
            System.Threading.Thread.Sleep(2000);

            driver.Quit();

        }

        [TestMethod]
        public void TestSignupAndBuyMembership()
        {
            var driver = InitialiseWebDriver();

            // 20170402 SA Sign up a new member
            string newEmail = "test" + Guid.NewGuid().ToString("N") + "@testtest.com";
            SignUpUser(driver, newEmail, "Password99!", "Associate");

            var element = driver.FindElement(By.CssSelector("input[type='submit']"));
            element.Click();
            System.Threading.Thread.Sleep(2000);


            FillCreditCardForm(driver);
            System.Threading.Thread.Sleep(2000);

            element = driver.FindElement(By.LinkText(newEmail));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            Assert.IsTrue(driver.PageSource.Contains("Associate membership expiry"));

            driver.Quit();

        }

        [TestMethod]
        public void TestSearchAndFindProduct()
        {
            var driver = InitialiseWebDriver();

            GoToProductsPage(driver);

            PerformSearch(driver, Guid.NewGuid().ToString("N"));
            Assert.IsTrue(
                driver.PageSource.Contains(
                    "Sorry, there are no products that meet your search criteria. Please try again."));

            PerformSearch(driver, "camera");
            Assert.IsTrue(driver.PageSource.Contains("Your search returned"));
        }

        [TestMethod]
        public void CheckNonLoggedInAccess()
        {
            var driver = InitialiseWebDriver();

            // 20170409 SA non members can go to the products page but when they go to buy a product, they have to log in
            AddProductToCart(driver, "Directional stereo Microphone XM0-E1");
            Assert.IsTrue(driver.Url.Contains("/login"));

            // 20170409 SA non members cannot go to the events page - they have to log in
            GoToHomePage(driver);
            GoToEventPage(driver);
            Assert.IsTrue(driver.Url.Contains("/login"));

            driver.Quit();

        }


        private static void GoToHomePage(IWebDriver driver)
        {
            driver.Navigate().GoToUrl(HomeUrl);
            System.Threading.Thread.Sleep(2000);

        }


        private static IWebDriver InitialiseWebDriver()
        {
            IWebDriver driver = new ChromeDriver();
            driver.Manage().Window.Size = new Size(1210, 800);
            System.Threading.Thread.Sleep(500);
            InitialiseDriver(driver);
            return driver;
        }

        private static void GoToProductsPage(IWebDriver driver)
        {
            var jse = (IJavaScriptExecutor) driver;
            jse.ExecuteScript("window.scrollBy(0,1500)", "");

            var element = driver.FindElement(By.CssSelector(".fig a[href='/products/index']"));
            System.Threading.Thread.Sleep(2000);
            element.Click();
            System.Threading.Thread.Sleep(2000);
        }

        private static void PerformSearch(IWebDriver driver, string term)
        {
            driver.FindElement(By.CssSelector(".open-search")).Click();
            System.Threading.Thread.Sleep(2000);

            var element = driver.FindElement(By.CssSelector(".search-input"));
            element.SendKeys(term);
            element.SendKeys(Keys.Return);
            System.Threading.Thread.Sleep(2000);
        }

        private void SignUpUser(IWebDriver driver, string email, string password, string membershiptype)
        {
            var element = driver.FindElement(By.Id("signupButton"));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            element = driver.FindElement(By.CssSelector("#signUpForm input[name='FirstName']"));
            element.SendKeys("Test");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#signUpForm input[name='LastName']"));
            element.SendKeys("User");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#signUpForm input[name='Email']"));
            element.SendKeys(email);
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#signUpForm input[name='Password']"));
            element.SendKeys(password);
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#MembershipType"));
            element.Click();
            System.Threading.Thread.Sleep(500);
            element.SendKeys(membershiptype);
            System.Threading.Thread.Sleep(500);
            element.Click();
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#signUpForm input[name='PhoneNumber']"));
            element.SendKeys("987654321");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#signUpForm textarea[name='Profile']"));
            element.SendKeys("This is a test profile");
            System.Threading.Thread.Sleep(500);

            if (membershiptype != "none")
            {
                element = driver.FindElement(By.CssSelector("#RangeId"));
                element.Click();
                System.Threading.Thread.Sleep(500);
                element.SendKeys("I");
                System.Threading.Thread.Sleep(500);
                element.Click();
                System.Threading.Thread.Sleep(500);
            }

            element.Submit();
            System.Threading.Thread.Sleep(2000);
        }

        private static void InitialiseDriver(IWebDriver driver)
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(100);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(100);
            driver.Navigate().GoToUrl(HomeUrl);
        }

        private static void LeaveReviews(IWebDriver driver)
        {
            IWebElement element;
            var elements = driver.FindElements(By.CssSelector("textarea[name]"));
            foreach (var textarea in elements)
            {
                textarea.SendKeys("this is a review for a product");
                System.Threading.Thread.Sleep(500);
            }

            element = driver.FindElement(By.CssSelector("input[value='Submit']"));
            element.Click();
            System.Threading.Thread.Sleep(2000);
        }

        private static void AddProductToCart(IWebDriver driver, string productName)
        {
            var jse = (IJavaScriptExecutor)driver;
            GoToProductsPage(driver);

            jse.ExecuteScript("window.scrollBy(0,500)", "");
            System.Threading.Thread.Sleep(2000);

            var element = driver.FindElement(By.LinkText(productName));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            element = driver.FindElement(By.CssSelector(".cartButton"));
            element.Click();
            System.Threading.Thread.Sleep(2000);
        }

        private static void CheckoutInShoppingCart(IWebDriver driver)
        {
            driver.FindElement(By.CssSelector("input[value='Checkout']")).Click();
            System.Threading.Thread.Sleep(2000);
        }

        private static void AddEventToCartAndCheckout(IWebDriver driver, string name)
        {
            GoToEventPage(driver);
            System.Threading.Thread.Sleep(3000);
            var element = driver.FindElement(By.LinkText(name));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            var jse = (IJavaScriptExecutor)driver;
            jse.ExecuteScript("window.scrollBy(0,1000)", "");
            System.Threading.Thread.Sleep(1000);
            element = driver.FindElement(By.CssSelector(".tix"));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            driver.FindElement(By.CssSelector("input[value='Checkout']")).Click();
            System.Threading.Thread.Sleep(2000);
        }

        private static void GoToEventPage(IWebDriver driver)
        {
            driver.Navigate().GoToUrl(HomeUrl + "events");
            System.Threading.Thread.Sleep(2000);
        }


        private static void FillCreditCardForm(IWebDriver driver)
        {
            var element = driver.FindElement(By.CssSelector("#firstname"));
            element.SendKeys("Sadia");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#lastname"));
            element.SendKeys("AFRIN");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#line1"));
            element.SendKeys("Templeton Place");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#city"));
            element.SendKeys("Auckland");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#postCode"));
            element.SendKeys("2103");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#country"));
            element.SendKeys("NZ");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#cardNumber"));
            element.SendKeys("4370511309717437");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#cardtype"));
            element.Click();
            System.Threading.Thread.Sleep(500);
            element.SendKeys("visa");
            System.Threading.Thread.Sleep(500);
            element.Click();
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#expire_month"));
            element.SendKeys("04");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#expire_year"));
            element.SendKeys("2022");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#cvv2"));
            element.SendKeys("111");
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("input[value='Pay']"));
            element.Click();
            System.Threading.Thread.Sleep(2000);
        }

        private void Login(IWebDriver driver, string username, string password)
        {
            var element = driver.FindElement(By.Id("loginButton"));
            element.Click();
            System.Threading.Thread.Sleep(2000);

            element = driver.FindElement(By.CssSelector("#login input[name='Email']"));
            element.SendKeys(username);
            System.Threading.Thread.Sleep(500);

            element = driver.FindElement(By.CssSelector("#login input[name='Password']"));
            element.SendKeys(password);
            System.Threading.Thread.Sleep(500);

            element.Submit();
            System.Threading.Thread.Sleep(2000);

        }
    }
}
