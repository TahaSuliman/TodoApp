using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IUserRepository : IRepository<Entities.User>
    {
        Task<Entities.User?> GetByEmailAsync(string email);
    }
}
