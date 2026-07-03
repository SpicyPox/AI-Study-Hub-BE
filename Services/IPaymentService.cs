using System.Threading.Tasks;
using AIStudyHub.Api.Models;

namespace AIStudyHub.Api.Services;

public interface IPaymentService
{
    Task<string> CreatePaymentUrlAsync(Transaction transaction, string ipAddress, string returnUrl);
}
