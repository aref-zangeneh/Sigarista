namespace Sugarista.Application.Affiliates.Services;

public interface IAffiliateQrCodeService
{
    string GenerateSvg(string content, int sizePx = 512);

    byte[] GeneratePng(string content, int sizePx = 1024);
}
