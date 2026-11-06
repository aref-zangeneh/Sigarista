using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.WalletPackages.DeleteWalletPackage;

public record DeleteWalletPackageCommand(Guid Id) : IRequest;

public class DeleteWalletPackageCommandHandler : IRequestHandler<DeleteWalletPackageCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteWalletPackageCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteWalletPackageCommand request, CancellationToken cancellationToken)
    {
        var package = await _dbContext.WalletTopUpPackages
            .FirstOrDefaultAsync(packageEntry => packageEntry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Wallet package was not found.");

        _dbContext.WalletTopUpPackages.Remove(package);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
