using MusicService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MusicService.Repository.Data.Configurations;

public class CategorySongConfiguration : IEntityTypeConfiguration<CategorySong>
{
    public void Configure(EntityTypeBuilder<CategorySong> builder)
    {
        builder.HasKey(cs => new { cs.CategoryId, cs.SongId });

        // Deleting a category or a song removes its rows here too.
        builder.HasOne(cs => cs.Category).WithMany(c => c.CategorySongs)
            .HasForeignKey(cs => cs.CategoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(cs => cs.Song).WithMany(s => s.CategorySongs)
            .HasForeignKey(cs => cs.SongId).OnDelete(DeleteBehavior.Cascade);
    }
}
