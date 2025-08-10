namespace CRM.ViewModel
{
    public class BulkUploadViewModel
    {
        public int CreatedByCode { get; set; }
        public string CreatedByName { get; set; }
        public int MaxNumberOfInterests { get; set; }
        public int? CurrentAcademicSettingId { get; set; }
    }

    public class BulkUploadResult
    {
        public int SuccessCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}