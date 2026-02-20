using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IPermissionManagement
    {
        Task<List<Permission>> GetPermissionAsync();
        Task<List<UserModel>> GetUserAsync();
        Task<List<MappingPermission>> GetMappingPermissionAsync(int UserID);
        Task<BaseResponseModel> UpdatePermissionAsync(UpdatePermissionModel model);
    }
}

