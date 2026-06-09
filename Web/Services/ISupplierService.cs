using Web.Models.Common;
using Web.Models.Suppliers;

namespace Web.Services;

public interface ISupplierService
{
    Task<PaginatedList<SupplierDto>> GetAllSuppliersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null);
    Task<SupplierDto?> GetSupplierByIdAsync(Guid id);
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request);
    Task<SupplierDto> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request);
    Task DeleteSupplierAsync(Guid id);
    Task<bool> CheckSupplierExistsAsync(string nameEn, string nameAr, Guid? excludeSupplierId = null);
    Task<bool> CheckSupplierEmailExistsAsync(string email, Guid? excludeSupplierId = null);
}
