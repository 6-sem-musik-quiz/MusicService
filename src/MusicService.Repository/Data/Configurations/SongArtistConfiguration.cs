using MusicService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MusicService.Repository.Data.Configurations;

public class SongArtistConfiguration : IEntityTypeConfiguration<SongArtist>
{
    public void Configure(EntityTypeBuilder<SongArtist> builder)
    {
        builder.HasKey(sa => new { sa.SongId, sa.ArtistId });

        builder.HasOne(sa => sa.Song).WithMany(s => s.SongArtists)
            .HasForeignKey(sa => sa.SongId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(sa => sa.Artist).WithMany(a => a.SongArtists)
            .HasForeignKey(sa => sa.ArtistId).OnDelete(DeleteBehavior.Cascade);
    }
}
