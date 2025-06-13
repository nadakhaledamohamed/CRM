using CRM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AcademicSettingsController : ControllerBase
    {
        private readonly CallCenterContext _context;

        public AcademicSettingsController(CallCenterContext context)
        {
            _context = context;
        }

        // GET: api/AcademicSettings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AcademicSetting>>> GetAcademicSettings()
        {
            return await _context.AcademicSettings.ToListAsync();
        }

        // GET: api/AcademicSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AcademicSetting>> GetAcademicSetting(int id)
        {
            var academicSetting = await _context.AcademicSettings.FindAsync(id);

            if (academicSetting == null)
            {
                return NotFound();
            }

            return academicSetting;
        }

        // PUT: api/AcademicSettings/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAcademicSetting(int id, AcademicSetting academicSetting)
        {
            if (id != academicSetting.AcademicSettingId)
            {
                return BadRequest();
            }

            _context.Entry(academicSetting).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AcademicSettingExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/AcademicSettings
        [HttpPost]
        public async Task<ActionResult<AcademicSetting>> PostAcademicSetting(AcademicSetting academicSetting)
        {
            _context.AcademicSettings.Add(academicSetting);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAcademicSetting", new { id = academicSetting.AcademicSettingId }, academicSetting);
        }

        // DELETE: api/AcademicSettings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAcademicSetting(int id)
        {
            var academicSetting = await _context.AcademicSettings.FindAsync(id);
            if (academicSetting == null)
            {
                return NotFound();
            }

            _context.AcademicSettings.Remove(academicSetting);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AcademicSettingExists(int id)
        {
            return _context.AcademicSettings.Any(e => e.AcademicSettingId == id);
        }
    }
}
