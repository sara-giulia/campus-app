using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("Offers");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Status).HasConversion<string>();
        builder.Ignore(o => o.RecipientId);

        // The offers of a conversation (listing + buyer), and the Pending ones of a listing.
        // "At most one Pending offer per buyer and listing" is checked by the handler, not by a unique
        // index: a counter-offer turns one row Countered and inserts the new Pending one in the same save.
        builder.HasIndex(o => new { o.ListingId, o.BuyerId });
        builder.HasIndex(o => new { o.ListingId, o.Status });
        builder.HasIndex(o => o.BuyerId);
        builder.HasIndex(o => o.SellerId);
        builder.HasIndex(o => o.ParentOfferId);

        // When the listing or one of the two members is deleted, the offers go too (like the conversation).
        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(o => o.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(o => o.BuyerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(o => o.SellerId)
            .OnDelete(DeleteBehavior.Cascade);

        // A counter-offer points to the offer it answers. The whole chain has the same listing, buyer and
        // seller, so the cascades above delete it at once; NO ACTION is checked at the end of that statement.
        builder.HasOne<Offer>()
            .WithMany()
            .HasForeignKey(o => o.ParentOfferId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
