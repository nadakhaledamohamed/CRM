using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Models;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Controllers
{
    public class LookupController : Controller
    {
        private readonly CallCenterContext _context;

        public LookupController(CallCenterContext context)
        {
            _context = context;
        }

        // High Schools
        public async Task<IActionResult> HighSchools()
        {
            return View(await _context.LookUpHighSchools.ToListAsync());
        }

        // Certificates
        public async Task<IActionResult> Certificates()
        {
            return View(await _context.LookUpHighSchoolCerts.ToListAsync());
        }

        // Status Types
        public async Task<IActionResult> StatusTypes()
        {
            return View(await _context.LookUpStatusTypes.ToListAsync());
        }

        // How Did You Know Us
        public async Task<IActionResult> HowDidYouKnowUs()
        {
            return View(await _context.LookUpHowDidYouKnowUs.ToListAsync());
        }

        // Majors
        public async Task<IActionResult> Majors()
        {
            return View(await _context.LookupMajors.ToListAsync());
        }

        // Roles
        public async Task<IActionResult> Roles()
        {
            return View(await _context.LookupRoles.ToListAsync());
        }
    }
}