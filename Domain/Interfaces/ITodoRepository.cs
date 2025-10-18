using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface ITodoRepository : IRepository<Entities.Todo>
    {
        Task<IEnumerable<Entities.Todo>> GetByUserIdAsync(int userId);
    }
}
