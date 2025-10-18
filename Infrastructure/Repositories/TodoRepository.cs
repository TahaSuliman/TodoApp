using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class TodoRepository : Repository<Todo>, ITodoRepository
    {
        public TodoRepository(AppDbContext context) : base(context) { }

        public override async Task<Todo?> GetByIdAsync(int id)
            => await _dbSet.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);

        public override async Task<IEnumerable<Todo>> GetAllAsync()
            => await _dbSet.Include(t => t.User).ToListAsync();

        public async Task<IEnumerable<Todo>> GetByUserIdAsync(int userId)
            => await _dbSet.Include(t => t.User).Where(t => t.UserId == userId).ToListAsync();
    }
}
