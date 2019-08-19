using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using InsuranceClient.Models;
using InsuranceClient.Models.ViewModels;
using System.IO;
using InsuranceClient.Helpers;
using Microsoft.Extensions.Configuration;

namespace InsuranceClient.Controllers
{
    public class HomeController : Controller
    {
        private IConfiguration configruation;
        public HomeController(IConfiguration configruation)
        {
            this.configruation = configruation;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(CustomeViewModel model)
        {
            if(ModelState.IsValid)
            {
                var customerId = Guid.NewGuid();
                StorageHelper storageHelper = new StorageHelper();
                storageHelper.ConnectString = configruation.GetConnectionString("StorageConnection");

                // save customer image to Azure blob
                var tempFile=Path.GetTempFileName();
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    await model.Image.CopyToAsync(fs);
                }
                var fileName = Path.GetFileName(model.Image.FileName);
                var tempPath = Path.GetDirectoryName(tempFile);
                var imagePath = Path.Combine(tempPath, string.Concat(customerId, "_", fileName));
                System.IO.File.Move(tempFile, imagePath); //rename temp file
                var imageUrl = await storageHelper.UploadCustomerImageAsync("image", imagePath);

                // save customer data to azure table
                Customer customer = new Customer( customerId.ToString(),model.InsuranceType);
                customer.FullName = model.FullName;
                customer.Email = model.Email;
                customer.Amount = model.Amount;
                customer.Premium = model.Premium;
                customer.AppDate = model.AppDate;
                customer.EndDate = model.EndDate;
                customer.ImageUrl = imageUrl;
                await storageHelper.InsertCustomerAsync("customer", customer);

                // add a confirmation message to azure queue
                await storageHelper.AddmessageAsync("insurance-requests", customer);

                return RedirectToAction("Index");

            }
            else
            {
                return View();
            }
            
        }

        public IActionResult Privacy()
        {
            return View();
        }


      

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
