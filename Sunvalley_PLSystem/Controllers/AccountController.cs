﻿using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Sunvalley_PLSystem.Models;
using System.Collections.Generic;
using System.Net;
using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Data;
using Sunvalley_PLSystem.GeneralTools;
using OfficeOpenXml;
using ProfitAndLossTotalConcepts = Sunvalley_PLSystem.Models.ApplicationUser.ProfitAndLossReport.ProfitAndLossTotals.TotalConcepts;
using static Sunvalley_PLSystem.Models.ApplicationUser.ProfitAndLossReport;
using static Sunvalley_PLSystem.GeneralTools.ExcelTools;
using OfficeOpenXml.Style;
using static Sunvalley_PLSystem.Models.House;

namespace Sunvalley_PLSystem.Controllers
{
    public class AccountController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager )
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

        // GET: Users
        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        public ActionResult Index(String status)
        {
            var allUsers = UserManager.Users.ToList();//Get all users
            var roles = new ApplicationDbContext().Roles.ToList();//Get all roles

            //Get a view showing users with their roles.
            var Users = from user in allUsers
                                     join rol in roles
                                     on user.Roles.First().RoleId equals rol.Id
                                     select new RegisterViewModel
                                     {
                                         firstName = user.firstName,
                                         lastName = user.lastName,
                                         country = user.country,
                                         mobilePhone = user.mobilePhone,
                                         Email = user.Email,
                                         roleName = rol.Name,
                                         registeredUserID = user.Id,
                                         status = user.status
                                     };

            if (status != null)
            {
                if (status == "Activate")
                {
                    Users = Users.Where(u => u.status == "Activate");
                    ViewBag.state = "Activate";
                }
                else
                {
                    Users = Users.Where(u => u.status == "Disable");
                    ViewBag.state = "Disable";
                }
            }
            else {
                Users = Users.Where(u => u.status == "Activate");
                ViewBag.state = "Activate";
            }

            return View(Users);
        }

        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        public ActionResult activateOrDiseable(String id)
        {
            //ApplicationUser user = new ApplicationUser();
            var user = UserManager.FindById<ApplicationUser, String>(id);
            var houses = db.Houses.Where(h => h.Id == id);
            if (user.status == "Activate")
            {
                foreach(var h in houses)
                {
                    h.status = false;
                }
                user.status = "Disable";
                UserManager.Update(user);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            else {
                foreach (var h in houses)
                {
                    h.status = true;
                }
                user.status = "Activate";
                UserManager.Update(user);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

        }

        // GET: Houses/Delete/5
        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        public ActionResult Delete(String id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ApplicationUser user = UserManager.FindById(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            return View(user);
        }

        // POST: Houses/Delete/5

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(String id)
        {
            ApplicationUser user = UserManager.FindById(id);
            UserManager.Delete(user);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        [Authorize]
        public ActionResult profitAndLossTotal(String id,DateTime? fecha1,DateTime? fecha2, bool onlyContributions = false)
        {
            String mensaje = db.GeneralInformations.Find(1).InformacionGen;
            var owner = db.Users.Find(id);
            ViewBag.NombreCompleto = owner.firstName + " " + owner.lastName;
            ViewBag.mensaje = mensaje;
            ViewBag.onlyContributions = onlyContributions;
            if (fecha1 == null && fecha2 == null)
            {
                List<Movement> m = new List<Movement>();
                ViewBag.Loss = m;
                ViewBag.Profit = m;
                ViewBag.Contribution = m;
                ViewBag.Tipo = "Total";
                ViewBag.firstSearch = true;
                ViewBag.Id = id;
                return View("profitAndLoss");
            }
            else {
                ViewBag.fecha1 = fecha1;
                ViewBag.fecha2 = fecha2;
                
                var listHouses = db.Houses.Where(h => h.Id == id);
                List<Movement> listMovement = new List<Movement>();
                var profitAndLossReport = owner.generateProfitAndLossReport(fecha1.Value, fecha2.Value);
                ViewBag.Loss = profitAndLossReport.lossReport;
                ViewBag.Profit = profitAndLossReport.profitReport;
                ViewBag.Contribution = profitAndLossReport.contributionReport;
                ViewBag.TotalLoss = profitAndLossReport.totals.FirstOrDefault(r => r.concept == ProfitAndLossTotalConcepts.TotalLoss).total;
                ViewBag.TotalProfit = profitAndLossReport.totals.FirstOrDefault(r => r.concept == ProfitAndLossTotalConcepts.TotalProfit).total;
                ViewBag.TotalContribution = profitAndLossReport.totals.FirstOrDefault(r => r.concept == ProfitAndLossTotalConcepts.TotalContribution).total;
                ViewBag.Total = profitAndLossReport.totals.FirstOrDefault(r => r.concept == ProfitAndLossTotalConcepts.Total).total;

                ViewBag.Id = id;
                return View("profitAndLoss", listMovement.ToList());
            }
        }


        [Authorize]
        public FileResult ProfitAndLossToExcel(String id, DateTime? fecha1, DateTime? fecha2, bool onlyContributions = false, int houseID = 0)
        {
            House house  = null;
            ApplicationUser owner;
            //Re-Create the report
            if (houseID == 0) //If house is not specified, just 
                owner = db.Users.Find(id);
            else //If house is specified, the owner is find from it
            {
                house = db.Houses.Find(houseID);
                owner = house.ApplicationUser;
            }
            ApplicationUser.ProfitAndLossReport profitAndLossReport = owner.generateProfitAndLossReport(fecha1.Value, fecha2.Value, houseID);

            //Pre-processed to be exported
            List<VMProfitAndLossRow> vmProfitList = VMProfitAndLossRow.listToVMMovements(profitAndLossReport.profitReport.ToList());
            DataTable dtProfit = ExcelTools.listToDatatable<VMProfitAndLossRow>(vmProfitList);

            List<VMProfitAndLossRow> vmLossList = VMProfitAndLossRow.listToVMMovements(profitAndLossReport.lossReport.ToList());
            DataTable dtLoss = ExcelTools.listToDatatable<VMProfitAndLossRow>(vmLossList);

            List<VMProfitAndLossRow> vmContribs = VMProfitAndLossRow.listToVMMovements(profitAndLossReport.contributionReport.ToList());
            DataTable dtContribs = ExcelTools.listToDatatable<VMProfitAndLossRow>(vmContribs);

            List<TableToExportExcel> datatables = new List<TableToExportExcel>();

            if (house != null) //If house was specified, house data is added to the Excel File
            {
                //Getting house data to exportr
                VMHouse vmHouse = house.getVM();
                var houseList = new List<VMHouse>();
                houseList.Add(vmHouse);
                DataTable dtHOuse = ExcelTools.listToDatatable<VMHouse>(houseList);
                datatables.Add(new TableToExportExcel(dtHOuse, "House"));
            }

            if (onlyContributions)
                datatables.Add(new TableToExportExcel(dtContribs, "Contributions"));
            else { 
                datatables.Add(new TableToExportExcel(dtProfit,"Income"));
                datatables.Add(new TableToExportExcel(dtLoss, "Loss"));
            }

            //Exported
            ExcelPackage package = ExcelTools.exportToExcel(datatables, onlyContributions? "Contributions" : "Profit and Loss");
            var reportTotals = profitAndLossReport.totals;
            ExcelWorksheet worksheet = package.Workbook.Worksheets.ElementAt(0);
            int startRow = worksheet.Dimension.End.Row+2;

            worksheet.Cells["A" + (startRow)].Value = "Totals";
            worksheet.Cells["A" + (startRow)].Style.Font.Size = 20;
            startRow++;
            int counter = startRow;
            foreach (var item in reportTotals)
            {
                if (onlyContributions && item.concept == ProfitAndLossTotalConcepts.TotalContribution
                    || !onlyContributions && (item.concept != ProfitAndLossTotalConcepts.TotalContribution)) { 
                    worksheet.Cells["A" + counter].Value = item.conceptName;
                    worksheet.Cells["B" + counter].Value = item.total;
                    counter++;
                }
            }
            // format cells - add borders  
            using (ExcelRange r = worksheet.Cells[startRow, 1, counter-1, 2])
            {
                r.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                r.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                r.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                r.Style.Border.Right.Style = ExcelBorderStyle.Thin;

                r.Style.Border.Top.Color.SetColor(System.Drawing.Color.Black);
                r.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.Black);
                r.Style.Border.Left.Color.SetColor(System.Drawing.Color.Black);
                r.Style.Border.Right.Color.SetColor(System.Drawing.Color.Black);
            }
            worksheet.InsertRow(1, 2);
            worksheet.Cells["A1"].Value = "From";
            worksheet.Cells["B1"].Value = fecha1.Value.ToString("dd/MMM/yyyy");
            worksheet.Cells["C1"].Value = "To";
            worksheet.Cells["D1"].Value = fecha2.Value.ToString("dd/MMM/yyyy");
            worksheet.Cells["A1"].Style.Font.Bold = true;
            worksheet.Cells["C1"].Style.Font.Bold = true;

            if(house == null) //House not specified, owner name is added in the header of the file
            {
                worksheet.Cells["E1"].Value = "Owner";
                worksheet.Cells["E1"].Style.Font.Bold = true;
                worksheet.Cells["F1"].Value = owner.OwnerName;
            }

            byte[] bytesFile = package.GetAsByteArray();
            return File(bytesFile, ExcelTools.EXCEL_MIME_TYPE,profitAndLossReport.ToString(onlyContributions)+ExcelTools.EXCEL_FORMAT);
        }

        [Authorize]
        public ActionResult profitAndLossHouse(int idHouse,DateTime? fecha1,DateTime? fecha2, bool onlyContributions = false)
        {
            String mensaje = db.GeneralInformations.Find(1).InformacionGen;
            String NombreCompleto = db.Houses.Find(idHouse).ApplicationUser.firstName + " " + db.Houses.Find(idHouse).ApplicationUser.lastName;
            String HAD = db.Houses.Find(idHouse).name + ", " + db.Houses.Find(idHouse).area + ", " + db.Houses.Find(idHouse).adress;
            String CCSP = db.Houses.Find(idHouse).cityArea + ", " + db.Houses.Find(idHouse).country + ", " + db.Houses.Find(idHouse).stateProvince + ", " + db.Houses.Find(idHouse).postalCode;
            ViewBag.HAD = HAD;
            ViewBag.CCSP = CCSP;
            ViewBag.NombreCompleto = NombreCompleto;
            ViewBag.mensaje = mensaje;
            ViewBag.onlyContributions = onlyContributions;

            var House = db.Houses.Find(idHouse);

            if (fecha2 != null)
            {
                fecha2 = fecha2.Value.AddHours(23).AddMinutes(59).AddSeconds(59);
            }

            if (User.IsInRole(ApplicationUser.RoleNames.ADMINISTRADOR))
            {
                ViewBag.Loss = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.EXPENSE 
                    && m.transactionDate >= fecha1 && m.transactionDate <= fecha2);
                ViewBag.Profit = House.movimientos.Where(m => (m.typeOfMovement == Movement.TypeOfMovements.INCOME || m.typeOfMovement == Movement.TypeOfMovements.TAX)
                    && m.transactionDate >= fecha1 && m.transactionDate <= fecha2);
                ViewBag.Contribution = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.CONTRIBUTION 
                    && m.transactionDate >= fecha1 && m.transactionDate <= fecha2);

                ViewBag.House = House;
            }
            else {
                ViewBag.Loss = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.EXPENSE 
                    && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true);
                ViewBag.Profit = House.movimientos.Where(m => (m.typeOfMovement == Movement.TypeOfMovements.INCOME || m.typeOfMovement == Movement.TypeOfMovements.TAX)
                    && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true);
                ViewBag.Contribution = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.CONTRIBUTION 
                    && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true);

                ViewBag.House = House;
            }
            if (fecha1 == null && fecha2 == null)
            {

                List<Movement> m = new List<Movement>();
                ViewBag.Tipo = "PorCasa";
                ViewBag.firstSearch = true;
                return View("profitAndLoss");

            }
            else {

                ViewBag.fecha1 = fecha1;
                ViewBag.fecha2 = fecha2;
                if (User.IsInRole("Administrador"))
                {
                    ViewBag.TotalLoss = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.EXPENSE 
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2).Sum(m => m.amount);
                    ViewBag.TotalProfit = House.movimientos.Where(m => (m.typeOfMovement == Movement.TypeOfMovements.INCOME || m.typeOfMovement == Movement.TypeOfMovements.TAX)
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2).Sum(m => m.amount);
                    ViewBag.TotalContribution = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.CONTRIBUTION 
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2).Sum(m => m.amount);

                    ViewBag.Total = House.movimientos.Where(m => (m.typeOfMovement == Movement.TypeOfMovements.INCOME || m.typeOfMovement == Movement.TypeOfMovements.TAX)
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2).
                        Sum(m => m.amount) - House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.EXPENSE 
                            && m.transactionDate >= fecha1 && m.transactionDate <= fecha2).
                        Sum(m => m.amount);
                    ViewBag.idHouse = idHouse;
                    return View("profitAndLoss", House.movimientos);
                }
                else
                {
                    ViewBag.TotalLoss = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.EXPENSE 
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true).Sum(m => m.amount);
                    ViewBag.TotalProfit = House.movimientos.Where(m => (m.typeOfMovement == Movement.TypeOfMovements.INCOME || m.typeOfMovement == Movement.TypeOfMovements.TAX)
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true).Sum(m => m.amount);
                    ViewBag.TotalContribution = House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.CONTRIBUTION 
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true).Sum(m => m.amount);

                    ViewBag.Total = House.movimientos.Where(m => (m.typeOfMovement == Movement.TypeOfMovements.INCOME || m.typeOfMovement == Movement.TypeOfMovements.TAX)
                        && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true)
                        .Sum(m => m.amount) 
                        -House.movimientos.Where(m => m.typeOfMovement == Movement.TypeOfMovements.EXPENSE 
                            && m.transactionDate >= fecha1 && m.transactionDate <= fecha2 && m.state == true)
                            .Sum(m => m.amount);
                    ViewBag.idHouse = idHouse;
                    return View("profitAndLoss", House.movimientos);
                }
            }
        }
        // POST: Houses/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ApplicationUser user, String NewPassword, String ConfirmPassword)
        {
            if (ModelState.IsValid && NewPassword == ConfirmPassword)
            {
                ApplicationDbContext context = new ApplicationDbContext();
                UserStore<ApplicationUser> store = new UserStore<ApplicationUser>(context);
                
                var usuario = store.FindByIdAsync(user.Id).Result;
                usuario.firstName = user.firstName;
                usuario.lastName = user.lastName;
                usuario.createAt = user.createAt;
                usuario.company = user.company;
                usuario.adress1 = user.adress1;
                usuario.adress2 = user.adress2;
                usuario.city = user.city;
                usuario.country = user.country;
                usuario.state = user.state;
                usuario.mobilePhone = user.mobilePhone;
                usuario.Email = user.Email.Trim();
                usuario.UserName = user.Email.Trim();
                usuario.otrosEmail = user.otrosEmail;
                usuario.postalCode = user.postalCode;
                usuario.businesFax = user.businesFax;

                //Si es administrador y se introdujo nuevo password
                if (User.IsInRole ("Administrador")&& !String.IsNullOrEmpty(NewPassword))
                {
                    UserManager<ApplicationUser> UserManager = new UserManager<ApplicationUser>(store);
                    //String userId = User.Identity.GetUserId();//"<YourLogicAssignsRequestedUserId>";
                    String hashedNewPassword = UserManager.PasswordHasher.HashPassword(NewPassword);
                    await store.SetPasswordHashAsync(usuario, hashedNewPassword);
                }

                await store.UpdateAsync(usuario);
                int updatedRegs = db.SaveChanges();
                return RedirectToAction("Index");
            }else if (NewPassword != ConfirmPassword)
            {
                ModelState.AddModelError("", "Password was not confirmed correctly.");
            }
            ViewBag.roleName = UserManager.GetRoles(user.Id).FirstOrDefault();
            return View(user);
        }

        // GET: Houses/Edit/5
        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        public ActionResult Edit(String id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ApplicationUser user = UserManager.FindById(id);
            ViewBag.roleName = UserManager.GetRoles(user.Id).FirstOrDefault();
            if (user == null)
            {
                return HttpNotFound();
            }
            return View(user);
        }

        // GET: Houses/Details/5
        [Authorize]
        public ActionResult Details(String id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ApplicationUser user = UserManager.FindById(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            return View(user);
        }
        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, change to shouldLockout: true

            var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);

            switch (result)
            {
                case SignInStatus.Success:
                    var user = UserManager.FindByEmail<ApplicationUser, String>(model.Email);
                    if (user.status != "Disable")
                    {
                        storeUsernameInSession(model.Email);
                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        ModelState.AddModelError("", "Your account was disabled. Please contact the administrator.");
                        return View(model);
                    }
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    return View(model);
            }
        }

        public String storeUsernameInSession(String email)
        {
            ApplicationUserManager userManager = this.UserManager;
            String NombreCompleto = userManager.FindByName(email).firstName + " " + UserManager.FindByName(email).lastName;
            Session["NombreCompleto"] = NombreCompleto;
            return NombreCompleto;
        }

        //
        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            // Require that the user has already logged in via username/password or external login
            if (!await SignInManager.HasBeenVerifiedAsync())
            {
                return View("Error");
            }
            return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/VerifyCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // The following code protects for brute force attacks against the two factor codes. 
            // If a user enters incorrect codes for a specified amount of time then the user account 
            // will be locked out for a specified amount of time. 
            // You can configure the account lockout settings in IdentityConfig
            var result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, isPersistent:  model.RememberMe, rememberBrowser: model.RememberBrowser);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(model.ReturnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid code.");
                    return View(model);
            }
        }

        //
        // GET: /Account/Register
        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        public ActionResult Register()
        {

            ApplicationDbContext dbRol = new ApplicationDbContext();
            SelectList lista = new SelectList(dbRol.Roles,"Id","Name");

            ViewBag.UserRoles= lista;
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [Authorize(Roles = ApplicationUser.RoleNames.ADMINISTRADOR)]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model,string UserRoles)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser(model);
                var result = await UserManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    //await SignInManager.SignInAsync(user, isPersistent:false, rememberBrowser:false);

                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                    // Send an email with this link
                    // string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    // var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                    // await UserManager.SendEmailAsync(user.Id, "Confirm your account", "Please confirm your account by clicking <a href=\"" + callbackUrl + "\">here</a>");
                    ////// aqui se utiliza el rol que recive  USER ROLES en el constructor y se hace una condicion que si es uno es admin.
                    if (UserRoles == "1")
                        UserManager.AddToRole(user.Id, "Administrador");
                    else
                        UserManager.AddToRole(user.Id, "Cliente");

                    //string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    //var callbackUrl = Url.Action("ConfirmEmail", "Account",
                    //   new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                    //await UserManager.SendEmailAsync(user.Id,
                    //   "Confirm your account", "Please confirm your account by clicking <a href=\""
                    //   + callbackUrl + "\">here</a>");

                    //// Uncomment to debug locally 
                    //TempData["ViewBagLink"] = callbackUrl;

                    //ViewBag.Message = "Check your email and confirm your account, you must be confirmed "
                    //                + "before you can log in.";

                    return RedirectToAction("Index", "Account");
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            //ModelState.AddModelError("", "very weak password please try another.");

            List<SelectListItem> lista = new List<SelectListItem>();
            bool sel = false;
            foreach (var r in db.Roles)
            {
                sel = r.Name == "Administrador" ? true : false;
                lista.Add(new SelectListItem
                {
                    Text = r.Name,
                    Selected = sel
                });
            }
            ViewBag.Roles = lista;
            return View(model);
        }

        //
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByNameAsync(model.Email);
                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return View("ForgotPasswordConfirmation");
                }

                // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                // Send an email with this link
                 string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                 var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);

                 String htmlBody = "<table><tr><td><img src='http://sunvalleyservicesrockypoint.com/wp-content/uploads/2016/06/weblogo.png' alt='Alternate Text' /></td></tr>"+
                     "<tr><td>Please reset your password by clicking <a href='"+ callbackUrl +"'>here</a></td></tr></table>";

                 await UserManager.SendEmailAsync(user.Id, "Reset Password", htmlBody);

                 return RedirectToAction("ForgotPasswordConfirmation", "Account");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            AddErrors(result);
            return View();
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            // Request a redirect to the external login provider
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        //
        // GET: /Account/SendCode
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();
            if (userId == null)
            {
                return View("Error");
            }
            var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);
            var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
            return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/SendCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            // Generate the token and send it
            if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
            {
                return View("Error");
            }
            return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
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

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Manage");
            }

            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
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


        private async Task<string> SendEmailConfirmationTokenAsync(string userID, string subject)
        {
            string code = await UserManager.GenerateEmailConfirmationTokenAsync(userID);
            var callbackUrl = Url.Action("ConfirmEmail", "Account",
               new { userId = userID, code = code }, protocol: Request.Url.Scheme);
            await UserManager.SendEmailAsync(userID, subject,
               "Please confirm your account by clicking <a href=\"" + callbackUrl + "\">here</a>");

            return callbackUrl;
        }


        //
        // POST: /Account/LogOff

        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut();
            //return RedirectToAction("Index", "Home");
            return RedirectToAction("LogIn", "Account");
        }

        //
        // GET: /Account/ExternalLoginFailure
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
                ModelState.AddModelError("", error);
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