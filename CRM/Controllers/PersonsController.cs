using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
//using Azure.Core;
using System.Linq;
using Request = CRM.Models.Request;
using System.Collections.Generic;
using CRM.FuncModels;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CRM.Controllers
{
    public class PersonsController : Controller
    {
        private readonly CallCenterContext _context;

        public PersonsController(CallCenterContext context)
        {
            _context = context;
        }

        // GET: Persons
        public async Task<IActionResult> Index()
        {
            

            return View(); // uses Views/Persons/Index.cshtml
        }


        //public async Task<IActionResult> GetAll()
        //{
        //    var people = await _context.People
        //        .Include(p => p.HighSchool)
        //        .Include(p => p.Certificate)
        //        .Include(p => p.HowDidYouKnowUs)
        //        .Include(p => p.Major)
        //        .Include(p => p.Requests)
        //        .ToListAsync();

        //    var personRequests = people.Select(p =>
        //    {
        //        var latestRequest = p.Requests?
        //            .OrderByDescending(r => r.CreatedAt)
        //            .FirstOrDefault();

        //        var createdByUser = _context.Users
        //            .FirstOrDefault(u => u.UserId == p.CreatedByCode);

        //        User? updatedByUser = null;
        //        if (p.UpdatedByCode.HasValue)
        //        {
        //            updatedByUser = _context.Users
        //                .FirstOrDefault(u => u.UserId == p.UpdatedByCode.Value);
        //        }

        //        // Null-check latestRequest before accessing its properties
        //        User? latestRequestCreatedByUser = null;
        //        if (latestRequest != null && latestRequest.CreatedByCode != null)
        //        {
        //            latestRequestCreatedByUser = _context.Users
        //                .FirstOrDefault(u => u.UserId == latestRequest.CreatedByCode);
        //        }

        //        User? latestRequestUpdatedByUser = null;
        //        if (latestRequest?.UpdatedByCode != null)
        //        {
        //            latestRequestUpdatedByUser = _context.Users
        //                .FirstOrDefault(u => u.UserId == latestRequest.UpdatedByCode.Value);
        //        }

        //        var updatedByName = latestRequestUpdatedByUser?.FullName ?? "N/A";

        //        return new PersonRequestViewModel
        //        {
        //            ID = latestRequest?.PersonId ?? 0, 
        //            FirstName = p.FirstName,
        //            LastName = p.LastName,
        //            Email = p.Email,
        //            Phone = p.Phone,
        //            NationalId = p.NationalId,
        //            UserType = p.UserType,
        //            HighSchoolId = p.HighSchoolId,
        //            CertificateId = p.CertificateId,
        //            MajorId = p.MajorId,
        //            HowDidYouKnowUsId = p.HowDidYouKnowUsId,
        //            Person_CreatedAt = p.CreatedAt,
        //            Person_CreatedByCode = p.CreatedByCode,
        //            Person_CreatedByName = createdByUser?.FullName ?? "Unknown",
        //            Person_UpdatedByCode = p.UpdatedByCode,
        //            Person_UpdatedAt = p.UpdatedAt,
        //            Person_UpdatedByName = updatedByUser?.FullName ?? "N/A",

        //            Request_UpdatedByName = updatedByName,
        //            Request_CreatedByName = latestRequestCreatedByUser?.FullName ?? "Unknown",
        //            Request_CreatedAt = latestRequest?.CreatedAt ?? DateTime.MinValue,
        //            Request_CreatedByCode = latestRequest?.CreatedByCode??0,
        //            Request_UpdatedAt = latestRequest?.UpdatedAt,
        //            Request_UpdatedByCode = latestRequest?.UpdatedByCode,
        //            Description = latestRequest?.Description ?? "N/A",
        //            Comments = latestRequest?.Comments ?? "N/A",
        //            FollowUpCount = latestRequest?.FollowUpCount ?? 0,
        //            LastFollowUpDate = latestRequest?.LastFollowUpDate,
        //            StatusId = latestRequest?.StatusId
        //        };
        //    }).ToList();

        //    return View(personRequests);
        //}



        // Updated controller method for advanced filtering and UI support
        public async Task<IActionResult> GetAll(
        [FromQuery]     List<FilterCondition> filters,
        string? searchTerm = null,
        string? email = null,
        string? phone = null,
        string? nationalId = null,
        int? userType = null,
        int? statusId = null,
        string? createdBy = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        string matchType = "and",
        int page = 1,
        int pageSize = 12,
        string sortBy = "CreatedAt",
        string sortOrder = "desc"
       )
        {
             
            var query = _context.People
                .Include(p => p.Requests)
                .AsQueryable();
            query = ApplyDynamicFilters(query, filters, matchType);
            var users = await _context.Users.ToListAsync();
            var userDictionary = users.ToDictionary(u => u.UserId, u => u.FullName);

           // var statuses = await _context.LookUpStatusTypes.ToDictionaryAsync(s => s.StatusId, s => s.StatusName);
            var statusOptions = await GetDropdownOptionsAsync("statusid");
            var userTypeOptions = await GetDropdownOptionsAsync("usertype");

            // Pass to view
            ViewBag.StatusOptions = statusOptions;
            ViewBag.UserTypeOptions = userTypeOptions;

            // MATCH TYPE LOGIC
            if (matchType == "or")
            {
                query = query.Where(p =>
                    (!string.IsNullOrWhiteSpace(searchTerm) && (
                        p.FirstName.Contains(searchTerm) ||
                        p.LastName.Contains(searchTerm) ||
                        p.Email.Contains(searchTerm) ||
                        p.Phone.Contains(searchTerm) ||
                        p.NationalId.Contains(searchTerm))) ||
                    (!string.IsNullOrWhiteSpace(email) && p.Email.Contains(email)) ||
                    (!string.IsNullOrWhiteSpace(phone) && p.Phone.Contains(phone)) ||
                    (!string.IsNullOrWhiteSpace(nationalId) && p.NationalId.Contains(nationalId)) ||
                    (userType.HasValue && p.UserType == userType));
            }
            else if (matchType == "not")
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    query = query.Where(p =>
                        !p.FirstName.Contains(searchTerm) &&
                        !p.LastName.Contains(searchTerm) &&
                        !p.Email.Contains(searchTerm) &&
                        !p.Phone.Contains(searchTerm) &&
                        !p.NationalId.Contains(searchTerm));
                if (!string.IsNullOrWhiteSpace(email))
                    query = query.Where(p => !p.Email.Contains(email));
                if (!string.IsNullOrWhiteSpace(phone))
                    query = query.Where(p => !p.Phone.Contains(phone));
                if (!string.IsNullOrWhiteSpace(nationalId))
                    query = query.Where(p => !p.NationalId.Contains(nationalId));
                if (userType.HasValue)
                    query = query.Where(p => p.UserType != userType);
            }
            else // matchType == "and"
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    query = query.Where(p =>
                        p.FirstName.Contains(searchTerm) ||
                        p.LastName.Contains(searchTerm) ||
                        p.Email.Contains(searchTerm) ||
                        p.Phone.Contains(searchTerm) ||
                        p.NationalId.Contains(searchTerm));
                if (!string.IsNullOrWhiteSpace(email))
                    query = query.Where(p => p.Email.Contains(email));
                if (!string.IsNullOrWhiteSpace(phone))
                    query = query.Where(p => p.Phone.Contains(phone));
                if (!string.IsNullOrWhiteSpace(nationalId))
                    query = query.Where(p => p.NationalId.Contains(nationalId));
                if (userType.HasValue)
                    query = query.Where(p => p.UserType == userType);
            }

            if (createdFrom.HasValue)
                query = query.Where(p => p.CreatedAt >= createdFrom);
            if (createdTo.HasValue)
                query = query.Where(p => p.CreatedAt <= createdTo.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(createdBy))
            {
                var matchingUserIds = users
                    .Where(u => u.FullName.Contains(createdBy, StringComparison.OrdinalIgnoreCase))
                    .Select(u => u.UserId)
                    .ToList();
                query = query.Where(p => matchingUserIds.Contains(p.CreatedByCode));
            }

            var people = await query.ToListAsync();

            var personRequests = people.Select(p =>
            {
                var latestRequest = p.Requests?
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();

                string statusName = "N/A";
                if (latestRequest?.StatusId is int sid && statusOptions.TryGetValue(sid, out var sname))
                {
                    statusName = sname;
                }

                return new PersonRequestViewModel
                {
                    ID = latestRequest?.RequestId ?? 0,
                    PersonID = p.PersonId,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Email = p.Email,
                    Phone = p.Phone,
                    NationalId = p.NationalId,
                    UserType = p.UserType,
                    Person_CreatedAt = p.CreatedAt,
                    Person_CreatedByCode = p.CreatedByCode,
                    Person_CreatedByName = userDictionary.GetValueOrDefault(p.CreatedByCode, "Unknown"),
                    Person_UpdatedByCode = p.UpdatedByCode,
                    Person_UpdatedAt = p.UpdatedAt,
                    Person_UpdatedByName = p.UpdatedByCode.HasValue
                        ? userDictionary.GetValueOrDefault(p.UpdatedByCode.Value, "N/A")
                        : "N/A",
                    Request_CreatedAt = latestRequest?.CreatedAt ?? DateTime.MinValue,
                    Request_CreatedByCode = latestRequest?.CreatedByCode ?? 0,
                    Request_CreatedByName = latestRequest?.CreatedByCode is int reqCreatedByCode
                        ? userDictionary.GetValueOrDefault(reqCreatedByCode, "Unknown")
                        : "Unknown",
                    Request_UpdatedAt = latestRequest?.UpdatedAt,
                    Request_UpdatedByCode = latestRequest?.UpdatedByCode,
                    Request_UpdatedByName = latestRequest?.UpdatedByCode is int reqUpdatedByCode
                        ? userDictionary.GetValueOrDefault(reqUpdatedByCode, "N/A")
                        : "N/A",
                    Description = latestRequest?.Description ?? string.Empty,
                    Comments = latestRequest?.Comments ?? string.Empty,
                    FollowUpCount = latestRequest?.FollowUpCount ?? 0,
                    LastFollowUpDate = latestRequest?.LastFollowUpDate,
                    StatusId = latestRequest?.StatusId,
                    StatusName = statusName
                };
            }).ToList();

            if (statusId.HasValue)
                personRequests = personRequests.Where(p => p.StatusId == statusId).ToList();

            personRequests = sortBy.ToLower() switch
            {
                "name" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.FirstName).ThenBy(p => p.LastName).ToList()
                    : personRequests.OrderByDescending(p => p.FirstName).ThenByDescending(p => p.LastName).ToList(),
                "email" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.Email).ToList()
                    : personRequests.OrderByDescending(p => p.Email).ToList(),
                "createdat" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.Person_CreatedAt).ToList()
                    : personRequests.OrderByDescending(p => p.Person_CreatedAt).ToList(),
                "createdby" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.Person_CreatedByName).ToList()
                    : personRequests.OrderByDescending(p => p.Person_CreatedByName).ToList(),
                _ => personRequests.OrderByDescending(p => p.Person_CreatedAt).ToList()
            };

            var totalCount = personRequests.Count;
            var paginatedResults = personRequests
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.Email = email;
            ViewBag.Phone = phone;
            ViewBag.NationalId = nationalId;
            ViewBag.UserType = userType;
            ViewBag.StatusId = statusId;
            ViewBag.CreatedBy = createdBy;
            ViewBag.CreatedFrom = createdFrom?.ToString("yyyy-MM-dd");
            ViewBag.CreatedTo = createdTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;
            ViewBag.MatchType = matchType;
            ViewBag.InitialFilters = filters;

            return View(paginatedResults);
        }
        private IQueryable<Person> ApplyTextFilter(IQueryable<Person> query, Expression<Func<Person, string>> selector, FilterCondition filter)
        {
            switch (filter.Operator)
            {
                case "contains":
                    return query.Where(p => EF.Functions.Like(EF.Property<string>(p, selector.GetPropertyAccess().Name), $"%{filter.Value}%"));
                case "not_contains":
                    return query.Where(p => !EF.Functions.Like(EF.Property<string>(p, selector.GetPropertyAccess().Name), $"%{filter.Value}%"));
                case "equals":
                    return query.Where(p => EF.Property<string>(p, selector.GetPropertyAccess().Name) == filter.Value);
                case "not_equals":
                    return query.Where(p => EF.Property<string>(p, selector.GetPropertyAccess().Name) != filter.Value);
                default:
                    return query;
            }
        }

        private IQueryable<Person> ApplyNumericFilter(IQueryable<Person> query, Expression<Func<Person, int>> selector, string op, int value)
        {
            switch (op)
            {
                case "equals":
                    return query.Where(p => EF.Property<int>(p, selector.GetPropertyAccess().Name) == value);
                case "not_equals":
                    return query.Where(p => EF.Property<int>(p, selector.GetPropertyAccess().Name) != value);
                default:
                    return query;
            }
        }

        private IQueryable<Person> ApplyDynamicFilters(IQueryable<Person> query, List<FilterCondition> filters, string matchType)
        {
            if (filters == null || !filters.Any()) return query;

            foreach (var filter in filters)
            {
                query = ApplyIndividualFilter(query, filter);
            }

            return query;
        }

        private IQueryable<Person> ApplyIndividualFilter(IQueryable<Person> query, FilterCondition filter)
        {
            switch (filter.Field.ToLower())
            {
                case "email":
                    return ApplyTextFilter(query, p => p.Email, filter);
                case "phone":
                    return ApplyTextFilter(query, p => p.Phone, filter);
                case "nationalid":
                    return ApplyTextFilter(query, p => p.NationalId, filter);
                case "usertype":
                    if (int.TryParse(filter.Value, out var userTypeVal))
                        return ApplyNumericFilter(query, p => p.UserType, filter.Operator, userTypeVal);
                    break;
                case "statusid":
                    if (int.TryParse(filter.Value, out var statusVal))
                    {
                        return filter.Operator switch
                        {
                            "equals" => query.Where(p => p.Requests.Any(r => r.StatusId == statusVal)),
                            "not_equals" => query.Where(p => !p.Requests.Any(r => r.StatusId == statusVal)),
                            _ => query
                        };
                    }
                    break;
            }
            return query;
        }
        [HttpGet]
        public async Task<Dictionary<int, string>> GetDropdownOptionsAsync(string field)
        {
            switch (field.ToLower())
            {
                case "usertype":
                    return new Dictionary<int, string>
            {
                { 1, "Applicant" },
                { 2, "Guardian" }
            };
                case "statusid":
                    return await _context.LookUpStatusTypes
                        .ToDictionaryAsync(s => s.StatusId, s => s.StatusName);
                default:
                    return new Dictionary<int, string>();
            }
        }
        private string GetPropertyName<T>(Expression<Func<Person, T>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
                return unaryMember.Member.Name;

            throw new ArgumentException("Invalid expression type");
        }


        // GET: Persons/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var person = await _context.People
                .Include(p => p.HighSchool)
                .Include(p => p.Certificate)
                .Include(p => p.Major)
                .Include(p => p.HowDidYouKnowUs)
                .FirstOrDefaultAsync(p => p.PersonId == id);

            if (person == null)
                return NotFound();

            var requests = await _context.Requests
                .Where(r => r.PersonId == id)
                .ToListAsync();

            var userDictionary = await _context.Users
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var requestViewModels = requests.Select(r => new RequestViewModel
            {
                RequestId = r.RequestId,
                Description = r.Description ?? "",
                Comments = r.Comments ?? "",
                FollowUpCount = r.FollowUpCount,
                LastFollowUpDate = r.LastFollowUpDate,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                CreatedByName = userDictionary.TryGetValue(r.CreatedByCode, out var created) ? created : "Unknown",
                UpdatedByName = r.UpdatedByCode.HasValue && userDictionary.TryGetValue(r.UpdatedByCode.Value, out var updated) ? updated : "N/A",
                StatusId = r.StatusId,
                StatusName = _context.LookUpStatusTypes.FirstOrDefault(s => s.StatusId == r.StatusId)?.StatusName ?? "N/A"
            }).ToList();

            var viewModel = new PersonDetailsViewModel
            {
                Person = person,
                Requests = requestViewModels
            };

            return View(viewModel);
        }

        public IActionResult Create()
        {
            ViewData["CertificateId"] = new SelectList(_context.LookUpHighSchoolCerts, "CertificateId", "CertificateName");
            ViewData["HighSchoolId"] = new SelectList(_context.LookUpHighSchools, "HighSchoolId", "HighSchoolName");
            ViewData["HowDidYouKnowUsId"] = new SelectList(_context.LookUpHowDidYouKnowUs, "HowDidYouKnowUsId", "HowDidYouKnowUs");
            ViewData["MajorId"] = new SelectList(_context.LookupMajors, "MajorId", "MajorInterest");
            ViewData["CreatedByCode"] = new SelectList(_context.Users, "UserId", "FullName");
            ViewData["UpdatedByCode"] = new SelectList(_context.Users, "UserId", "FullName");
            ViewData["StatusId"] = new SelectList( _context.LookUpStatusTypes, "StatusId", "StatusName");

            return View();
        }

        // POST: Persons/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PersonId,FirstName,LastName,Email,Phone,HighSchoolId,CertificateId,HowDidYouKnowUsId,MajorId,UserType,NationalId,CreatedByCode,CreatedAt,UpdatedByCode,UpdatedAt")] Person person)
        {
            if (person.HighSchoolId == 0)
            {
                ModelState.AddModelError("HighSchoolId", "Please select a high school");
            }

            if (person.CertificateId == 0)
            {
                ModelState.AddModelError("CertificateId", "Please select a certificate type");
            }
            if (ModelState.IsValid)
            {
                _context.Add(person);
                await _context.SaveChangesAsync();
                //return RedirectToAction("Index", "Home");
                //return RedirectToAction(nameof(Index));
                return LocalRedirect("~/");
            }
            // Log validation errors
            foreach (var error in ModelState)
            {
                if (error.Value.Errors.Count > 0)
                {
                    Console.WriteLine($"Field: {error.Key}, Error: {error.Value.Errors[0].ErrorMessage}");
                }
            }
            ViewData["CertificateId"] = new SelectList(_context.LookUpHighSchoolCerts, "CertificateId", "CertificateName", person.CertificateId);
            ViewData["HighSchoolId"] = new SelectList(_context.LookUpHighSchools, "HighSchoolId", "HighSchoolName", person.HighSchoolId);
            ViewData["HowDidYouKnowUsId"] = new SelectList(_context.LookUpHowDidYouKnowUs, "HowDidYouKnowUsId", "HowDidYouKnowUs", person.HowDidYouKnowUsId);
            ViewData["MajorId"] = new SelectList(_context.LookupMajors, "MajorId", "MajorInterest", person.MajorId);
            ViewData["CreatedByCode"] = new SelectList(_context.Users, "UserId", "FullName", person.CreatedByCode);
            ViewData["UpdatedByCode"] = new SelectList(_context.Users, "UserId", "FullName", person.UpdatedByCode);

            return View();
        }

       
        // GET: Persons/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var person = await _context.People
                .Include(p => p.Certificate)
                .Include(p => p.HighSchool)
                .Include(p => p.HowDidYouKnowUs)
                .Include(p => p.Major)
                .FirstOrDefaultAsync(m => m.PersonId == id);

            if (person == null)
            {
                return NotFound();
            }

            return View(person);
        }

        // POST: Persons/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var person = await _context.People.FindAsync(id);
            _context.People.Remove(person);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PersonExists(int id)
        {
            return _context.People.Any(e => e.PersonId == id);
        }



        [HttpGet]
        public async Task<IActionResult> CreateWithRequest()
        {
            var model = new PersonRequestViewModel();

            // Get current user info
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            // Set the display values for the form
            model.Person_CreatedByCode = currentUserId;
            model.Person_CreatedByName = currentUserName;

            await LoadSelectLists(model);
            return View(model);
        }


        // POST: Persons/CreateWithRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
     
        public async Task<IActionResult> CreateWithRequest(PersonRequestViewModel model)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            // Always set these values before validation
            model.Person_CreatedByCode = currentUserId;
            model.Request_CreatedByCode = currentUserId;
            model.Person_CreatedByName = currentUserName;
            model.Request_CreatedByName = currentUserName;
            if (!ModelState.IsValid)
            {
                await LoadSelectLists(model); // Load dropdowns again
                return View("Create", model);

            }

            // using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                
                // var currentUserId = 1;
                var person = new Person
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Phone = model.Phone,
                    NationalId = model.NationalId,
                    UserType = model.UserType,
                    HighSchoolId = model.HighSchoolId ?? 0,
                    CertificateId = model.CertificateId ?? 0,
                    MajorId = model.MajorId ?? 0,
                    HowDidYouKnowUsId = model.HowDidYouKnowUsId ?? 0,
                    CreatedAt=DateTime.Now,
                    // CreatedAt = model.Person_CreatedAt != default ? model.Person_CreatedAt : DateTime.Now,
                    CreatedByCode = model.Person_CreatedByCode > 0 ? model.Person_CreatedByCode : currentUserId
                    
                    //UpdatedAt = DateTime.Now,
                    //UpdatedByCode = currentUserId
                };

                _context.People.Add(person);
                await _context.SaveChangesAsync();

                // Create Request
                var request = new Request
                {
                    PersonId = person.PersonId,
                    Description = model.Description,
                    Comments = model.Comments,
                    FollowUpCount = model.FollowUpCount,
                    //LastFollowUpDate = model.LastFollowUpDate,
                    CreatedAt = DateTime.Now,
                    CreatedByCode = currentUserId,
                    //UpdatedAt = DateTime.Now,
                    //UpdatedByCode = currentUserId,
                    StatusId = model.StatusId ?? 0
                };

                _context.Requests.Add(request);
                await _context.SaveChangesAsync();

        
                
              //  await transaction.CommitAsync();

                return RedirectToAction("Details", new { id = person.PersonId });
            }
            catch (Exception ex)
            {
                //await transaction.RollbackAsync();
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                // Optionally log the error
                await LoadSelectLists(model); // Reload dropdowns
                return View("Create", model);

            }
        }

        private async Task LoadSelectLists(PersonRequestViewModel model)
        {
            ViewData["CertificateId"] = new SelectList(await _context.LookUpHighSchoolCerts.ToListAsync(), "CertificateId", "CertificateName", model.CertificateId);
            ViewData["HighSchoolId"] = new SelectList(await _context.LookUpHighSchools.ToListAsync(), "HighSchoolId", "HighSchoolName", model.HighSchoolId);
            ViewData["HowDidYouKnowUsId"] = new SelectList(await _context.LookUpHowDidYouKnowUs.ToListAsync(), "HowDidYouKnowUsId", "HowDidYouKnowUs", model.HowDidYouKnowUsId);
            ViewData["MajorId"] = new SelectList(await _context.LookupMajors.ToListAsync(), "MajorId", "MajorInterest", model.MajorId);
            ViewData["StatusId"] = new SelectList(await _context.LookUpStatusTypes.ToListAsync(), "StatusId", "StatusName", model.StatusId);

            ViewData["UserType"] = new SelectList(new List<SelectListItem>
            {
                 new SelectListItem { Value = "1", Text = "Applicant" },
                new SelectListItem { Value = "2", Text = "Parent" }
              }, "Value", "Text", model.UserType);


        }

        //////////////edit 
        ///
        [HttpGet]
        public async Task<IActionResult> EditWithRequest(int id)
        {
            var person = await _context.People
                .Include(p => p.Requests)
                .FirstOrDefaultAsync(p => p.PersonId == id);

            if (person == null)
                return NotFound();

            var userDictionary = await _context.Users
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var model = new PersonRequestViewModel
            {
                PersonID = person.PersonId,
                FirstName = person.FirstName,
                LastName = person.LastName,
                Email = person.Email,
                Phone = person.Phone,
                NationalId = person.NationalId,
                UserType = person.UserType,
                HighSchoolId = person.HighSchoolId,
                CertificateId = person.CertificateId,
                MajorId = person.MajorId,
                HowDidYouKnowUsId = person.HowDidYouKnowUsId,
                Person_UpdatedAt = person.UpdatedAt,
                Person_UpdatedByCode = person.UpdatedByCode,
                Person_UpdatedByName = person.UpdatedByCode.HasValue && userDictionary.TryGetValue(person.UpdatedByCode.Value, out var pUpdater) ? pUpdater : "N/A",
                Description = person.Requests.FirstOrDefault()?.Description ?? "",
                Comments = person.Requests.FirstOrDefault()?.Comments ?? "",
                FollowUpCount = person.Requests.FirstOrDefault()?.FollowUpCount ?? 0,
                StatusId = person.Requests.FirstOrDefault()?.StatusId,
                Request_UpdatedAt = person.Requests.FirstOrDefault()?.UpdatedAt,
                Request_UpdatedByCode = person.Requests.FirstOrDefault()?.UpdatedByCode,
                Request_UpdatedByName = person.Requests.FirstOrDefault()?.UpdatedByCode.HasValue == true &&
                                        userDictionary.TryGetValue(person.Requests.FirstOrDefault()!.UpdatedByCode!.Value, out var rUpdater)
                                        ? rUpdater : "N/A",

                Requests = person.Requests.Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    Description = r.Description ?? string.Empty, // Ensure Description is never null
                    Comments = r.Comments,
                    FollowUpCount = r.FollowUpCount,
                    StatusId = r.StatusId,
                    UpdatedAt = r.UpdatedAt,
                    UpdatedByCode = r.UpdatedByCode ?? 0, // safely use 0 as fallback
                    UpdatedByName = r.UpdatedByCode.HasValue && userDictionary.TryGetValue(r.UpdatedByCode.Value, out var updatedName)
                        ? updatedName : "N/A"
                }).ToList()
            };
            for (int i = 0; i < model.Requests.Count; i++)
            {
                if (string.IsNullOrEmpty(model.Requests[i].Description))
                {
                    Console.WriteLine($"Request {i} has empty description");
                    // Set a default value if empty
                    model.Requests[i].Description = "No description provided";
                }
            }
            await LoadSelectLists(model);
            return View("EditWithRequest", model);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWithRequest(PersonRequestViewModel model)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).ToList();
                await LoadSelectLists(model);
                return View("EditWithRequest", model);
            }

            var person = await _context.People
                .Include(p => p.Requests)
                .FirstOrDefaultAsync(p => p.PersonId == model.PersonID);

            if (person == null)
                return NotFound();

            // Update person
            person.FirstName = model.FirstName;
            person.LastName = model.LastName;
            person.Email = model.Email;
            person.Phone = model.Phone;
            person.NationalId = model.NationalId;
            person.UserType = model.UserType;
            person.HighSchoolId = model.HighSchoolId ?? 0;
            person.CertificateId = model.CertificateId ?? 0;
            person.MajorId = model.MajorId ?? 0;
            person.HowDidYouKnowUsId = model.HowDidYouKnowUsId ?? 0;
            person.UpdatedAt = DateTime.Now;
            person.UpdatedByCode = currentUserId;

            // Update each request from the form
            foreach (var requestVm in model.Requests)
            {
                var existingRequest = person.Requests.FirstOrDefault(r => r.RequestId == requestVm.RequestId);
                if (existingRequest != null)
                {
                    existingRequest.Description = requestVm.Description;
                    existingRequest.Comments = requestVm.Comments;
                    existingRequest.FollowUpCount = requestVm.FollowUpCount;
                    existingRequest.StatusId = requestVm.StatusId ?? 0;
                    existingRequest.UpdatedAt = DateTime.Now;
                    existingRequest.UpdatedByCode = currentUserId;
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = model.PersonID });
        }







    }
}
