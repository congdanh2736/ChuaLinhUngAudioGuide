using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;

namespace AudioGuide.Api.Repositories
{
    public class PaymentRepository : Repository<Payment>, IPaymentRepository
    {
        public PaymentRepository(ApplicationDbContext context) : base(context) { }
    }
}
