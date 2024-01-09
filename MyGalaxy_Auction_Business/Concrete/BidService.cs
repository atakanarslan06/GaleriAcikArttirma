using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MyGalaxy_Auction_Business.Abstraction;
using MyGalaxy_Auction_Business.Dtos;
using MyGalaxy_Auction_Core.MailHelper;
using MyGalaxy_Auction_Core.Models;
using MyGalaxy_Auction_Data_Access.Context;
using MyGalaxy_Auction_Data_Access.Domain;

namespace MyGalaxy_Auction_Business.Concrete
{
    public class BidService : IBidService
    {
        private readonly ApplicationDbContext context;
        private readonly IMapper mapper;
        private readonly ApiResponse response;
        private readonly IMailService _mailService;

        public BidService(ApplicationDbContext context,IMailService mailService,IMapper mapper,ApiResponse response)
        {
            this.context = context;
            this.mapper = mapper;
            this.response = response;
            _mailService = mailService;
        }

        public async Task<ApiResponse> AutomaticallyCreateBid(CreateBidDTO model)
        {
            // Kullanıcının belirli bir araç için ödeme yapmış olup olmadığını kontrol eder
            var isPaid = await CheckIsPaidAuction(model.UserId, model.VehicleId);

            // Eğer kullanıcı ödeme yapmamışsa, işlem başarısız olur ve uygun bir hata mesajı eklenerek yanıt döndürülür
            if (!isPaid)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add("Please before pay auction price");
                return response;
            }

            // Aktif olan bir araç için mevcut en yüksek teklifi getirir
            var result = await context.Bids
                .Where(x => x.VehicleId == model.VehicleId && x.Vehicle.IsActive == true)
                .OrderByDescending(x => x.BidAmount)
                .ToListAsync();

            // Eğer mevcut en yüksek teklif bulunamazsa, işlem başarısız olur ve uygun bir hata mesajı eklenerek yanıt döndürülür
            if (result.Count == 0)
            {
                response.isSuccess = false;
                return response;
            }

            // Yeni bir teklif nesnesi oluşturulur ve CreateBidDTO modelinden haritalama yapılır
            var objDTO = mapper.Map<Bid>(model);

            // Yeni teklifin tutarını belirler. Mevcut en yüksek teklifin %10 üstüne eklenir
            objDTO.BidAmount = result[0].BidAmount + (result[0].BidAmount * 10) / 100;

            // Yeni teklifin tarihini şu anki zamana ayarlar
            objDTO.BidDate = DateTime.Now;

            // Yeni teklif veritabanına eklenir
            context.Bids.Add(objDTO);

            // Değişiklikler veritabanına kaydedilir
            await context.SaveChangesAsync();

            // Yanıt nesnesine başarılı olduğunu işaretler
            response.isSuccess = true;

            // Yanıtın sonucunu mevcut teklif listesi olarak belirler
            response.Result = result;

            // Yanıtı döndürür
            return response;


        }

        public Task<ApiResponse> CancelBid(int bidId)
        {
            throw new NotImplementedException();
        }

        public async Task<ApiResponse> CreateBid(CreateBidDTO model)
        {
            // Aktif olup olmadığını kontrol etmek için ilgili aracın durumunu sorgular
            var returnValue = await CheckIsActive(model.VehicleId);

            // Kullanıcının ilgili araç için ödeme yapmış olup olmadığını kontrol eder
            var isPaid = await CheckIsPaidAuction(model.UserId, model.VehicleId);

            // Kullanıcı ödeme yapmamışsa, işlem başarısız olur ve uygun bir hata mesajı eklenerek yanıt döndürülür
            if (!isPaid)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add("Please before pay auction price");
                return response;
            }

            // Aracın aktif olup olmadığını kontrol eder; eğer araç aktif değilse, işlem başarısız olur ve uygun bir hata mesajı eklenerek yanıt döndürülür
            if (returnValue == null)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add("this car is not active");
                return response;
            }

            // Kullanıcının verdiği teklifin aracın varsayılan fiyatını geçip geçmediğini kontrol eder
            if (returnValue.Price >= model.BidAmount)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add($"You should surpass the default price for this car {returnValue.Price}");
                return response;
            }
            if (model != null)
            {
                var topPrice = await context.Bids.Where(x => x.VehicleId == model.VehicleId).OrderByDescending(x => x.BidAmount).ToListAsync();
                if (topPrice.Count != 0)
                {
                    if (topPrice[0].BidAmount >= model.BidAmount && model.BidAmount < topPrice[0].BidAmount + (topPrice[0].BidAmount * 1) / 100) 
                    {
                        response.isSuccess = false;
                        response.ErrorMessages.Add("Entry bid amount,not lower than higher price to the system; higher price is : " + topPrice[0].BidAmount + (topPrice[0].BidAmount * 1) / 100);
                        return response;
                    }
                }
                Bid bid = mapper.Map<Bid>(model);
                bid.BidDate = DateTime.Now;
                await context.Bids.AddAsync(bid);
                if (await context.SaveChangesAsync()>0)
                {
                    var userDetail = await context.Bids.Include(x=>x.User).Where(x => x.UserId == model.UserId).FirstOrDefaultAsync();
                    _mailService.SendEmail("Your bid is success", "Your bid is :" + bid.BidAmount, bid.User.UserName);
                    response.isSuccess = true;
                    response.Result = model;
                    return response;
                }
             
            }
            response.isSuccess = false;
            response.ErrorMessages.Add("Ooops! sometihng went wrong");
            return response;
        }

        public async Task<ApiResponse> GetBidById(int bidId)
        {
            var result = await context.Bids.Include(x=>x.User).Where(x => x.BidId == bidId).FirstOrDefaultAsync();
            if(result == null)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add("bid is not found");
                return response;
            }

            response.isSuccess = true;
            response.Result = result;
            return response;
        
        
        }

        public async Task<ApiResponse> GetBidByVehicleId(int vehicleId)
        {
            var obj = await context.Bids.Include(x=>x.Vehicle).ThenInclude(x=>x.Bids).Where(x => x.VehicleId == vehicleId).ToListAsync();
            if (obj!=null)
            {
                response.isSuccess = true;
                response.Result = obj;
                return response;
            }
            response.isSuccess = false;
            return response;
        }

        public async Task<ApiResponse> UpdateBid(int bidId, UpdateBidDTO model)
        {
            //Update eden kullanıcı en son verdiği teklifin üzerine çıkmalıdır.
            var isPaid = await CheckIsPaidAuction(model.UserId, model.VehicleId);
            if (!isPaid)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add("Please before pay auction price");
                return response;
            }
            var result = await context.Bids.FindAsync(bidId);
            if (result == null)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add("bid is not found");
                return response;
            }
            if (result.BidAmount < model.BidAmount && result.UserId == model.UserId)
            {
                var objDTO = mapper.Map(model, result);
                objDTO.BidDate = DateTime.Now;
                response.isSuccess = true;
                response.Result = objDTO;
                await context.SaveChangesAsync();
                return response;
            }
            else if(result.BidAmount >= model.BidAmount)
            {
                response.isSuccess = false;
                response.ErrorMessages.Add("You are not entry low price than your old bid amount,your older bid amount is : " + result.BidAmount);
                return response;
            }
            response.isSuccess = false;
            response.ErrorMessages.Add("Something went wrong");
            return response;

        }



        // Verilen vehicleId ile eşleşen ve aktif olan bir aracı sorgular
        private async Task<Vehicle> CheckIsActive(int vehicleId)
        {
            // Veritabanından, verilen vehicleId'ye sahip ve aktif olan bir aracı getirir.
            // Ayrıca aracın EndTime'ı şu andan büyük olmalıdır.
            var obj = await context.Vehicles
                .Where(x => x.VehicleId == vehicleId && x.IsActive == true && x.EndTime >= DateTime.Now)
                .FirstOrDefaultAsync();

            // Eğer böyle bir araç bulunursa (obj null değilse), bu aracı döndürür.
            if (obj != null)
            {
                return obj;
            }

            // Eğer verilen vehicleId ile eşleşen bir araç bulunamazsa veya bulunan araç aktif değilse, null döndürür.
            return null;
        }

        // Bu metot, bir kullanıcının belirli bir araç için ödeme yapmış olup olmadığını kontrol eder.
        private async Task<bool> CheckIsPaidAuction(string userId, int vehicleId)
        {
            // Ödeme geçmişini veritabanından sorgular
            var obj = await context.PaymentHistories
                .Where(x => x.UserId == userId && x.VehicleId == vehicleId && x.IsActive == true)
                .FirstOrDefaultAsync();

            // Eğer ödeme geçmişi bulunursa (obj null değilse), kullanıcı ödeme yapmıştır ve true döndürülür.
            if (obj != null)
            {
                return true;
            }

            // Eğer ödeme geçmişi bulunamazsa veya bulunan geçmiş aktif değilse, kullanıcı ödeme yapmamıştır ve false döndürülür.
            return false;
        }




    }
}
