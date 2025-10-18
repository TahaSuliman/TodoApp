using Application.Common;
using Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public interface ITodoService
    {
        Task<Result<IEnumerable<TodoDto>>> GetAllAsync();
        Task<Result<TodoDto>> GetByIdAsync(int id);
        Task<Result<IEnumerable<TodoDto>>> GetByUserIdAsync(int userId);
        Task<Result<TodoDto>> CreateAsync(CreateTodoDto dto);
        Task<Result<bool>> UpdateAsync(UpdateTodoDto dto);
        Task<Result<bool>> DeleteAsync(int id);
        Task<Result<bool>> ToggleCompleteAsync(int id);
    }
}
