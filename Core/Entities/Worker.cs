namespace ERPES.ControlTower.Core.Entities;

public class Worker
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;

    public int HomePlantId { get; set; }
    public Plant? HomePlant { get; set; }

    public int CurrentPlantId { get; set; }
    public Plant? CurrentPlant { get; set; }

    public string RoleOrSkill { get; set; } = string.Empty;
    public int SkillScore { get; set; }
    public bool IsMobilityAccepted { get; set; }
    public decimal MobilityAllowanceRate { get; set; }
    public string Status { get; set; } = "Active";
}
