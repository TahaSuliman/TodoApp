using Application.Common;
using Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public interface IUserService
    {
        //Task<Result<IEnumerable<UserDto>>> GetAllAsync();
        //Task<Result<UserDto>> GetByIdAsync(int id);
        //Task<Result<UserDto>> CreateAsync(CreateUserDto dto);

        Task<Result<IEnumerable<UserDto>>> GetAllAsync();
        Task<Result<UserDto>> GetByIdAsync(int id);
        Task<Result<UserDto>> CreateAsync(CreateUserDto dto);
        Task<Result<bool>> UpdateAsync(EditUserDto dto);
        Task<Result<bool>> DeleteAsync(int id);
    }
}
