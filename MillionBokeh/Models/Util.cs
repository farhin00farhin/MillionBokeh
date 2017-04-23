using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Security.Claims;
using System.Security.Principal;
using ImageProcessor;
using ImageProcessor.Imaging;
using Microsoft.AspNet.Identity;

namespace MillionBokeh.Models
{
    public class Utilities
    {
        // 20170402 SA This class contains methods to be reused in other parts of code

        private static MillionBokehEntities db = new MillionBokehEntities();

        public static bool IsUserAdmin(IPrincipal user)
        {
            return user.IsInRole("Admin");
        }

        public static string GetLoggedInUserId(IPrincipal user)
        {
            Claim userIdClaim = null;
            var claimsIdentity = user.Identity as ClaimsIdentity;
            if (claimsIdentity != null)
            {
                userIdClaim = claimsIdentity.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            }
            if (userIdClaim != null)
            {
                return userIdClaim.Value;
            }
            return string.Empty;
        }

        public static AspNetUser GetMember(IPrincipal user)
        {
            string currentUserId = GetLoggedInUserId(user);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return null;
            }

            return db.AspNetUsers.Find(currentUserId);

        }

        public static bool IsMembershipValid(AspNetUser member)
        {
            if (member == null)
            {
                return false;
            }
            if (member.MemberType < 2)
            {
                // 20170317 SA If they are a non member, return false
                return false;
            }

            if (member.LastRenewalDate.HasValue)
            {
                if ((DateTime.Now - member.LastRenewalDate).Value.TotalDays > 365)
                {
                    // 20170317 SA If they are an expired member, return false
                    return false;
                }
            }
            else
            {
                // 20170317 SA If they have never paid for their membership, return false
                return false;
            }

            // 20170317 SA Passed all the checks - return true
            return true;
        }

        // 20170324 SA Used to crop imges to the right size while resizing it as much as possible to keep the picture as it was
        public static void ProcessImage(string imagePath, string outputPath, int height, int width)
        {
            byte[] photoBytes = File.ReadAllBytes(imagePath); 

            using (var inStream = new MemoryStream(photoBytes))
            {
                using (var outStream = new MemoryStream())
                {
                    using (var imageFactory = new ImageFactory(preserveExifData: true))
                    {
                        ResizeLayer resizeLayer = new ResizeLayer(new Size(width, height), ResizeMode.Crop, AnchorPosition.Center);
                        imageFactory.Load(inStream)
                            .Resize(resizeLayer)
                            .Save(outStream);

                        using (FileStream file = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                        {
                            byte[] bytes = new byte[outStream.Length];
                            outStream.Read(bytes, 0, (int)outStream.Length);
                            file.Write(bytes, 0, bytes.Length);
                            outStream.Close();
                        }
                    }
                }
            }
        }

        public static double CalculateDistance(double p1Lat, double p1Long, double p2Lat, double p2Long)
        {
                var R = 6378137; // Earth’s mean radius in meter
                var dLat = Rad(p2Lat - p1Lat);
                var dLong = Rad(p2Long - p1Long);
                var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                  Math.Cos(Rad(p1Lat)) * Math.Cos(Rad(p2Lat)) *
                  Math.Sin(dLong / 2) * Math.Sin(dLong / 2);
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                var d = R * c;
                return d; // returns the distance in meter
        }

        public static double Rad(double x)
        {
                return x * Math.PI / 180;
        }
    }
}