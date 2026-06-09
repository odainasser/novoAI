namespace Domain.Entities;

public class UnitUnitType
{
    public Guid UnitId { get; set; }
    public virtual Unit? Unit { get; set; }
    public Guid UnitTypeId { get; set; }
    public virtual Lookup? UnitType { get; set; }
}
