using MusicService.Domain.Entities;
using MusicService.Repository.Data;
using MusicService.Repository.Interfaces;

namespace MusicService.Repository.Repositories;

public class ExampleRepository(AppDbContext context) : Repository<Example>(context), IExampleRepository
{
}
