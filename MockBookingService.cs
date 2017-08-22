using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using E = System.Linq.Enumerable;

namespace testSibiraBot.Services
{
    [Serializable]
    public class MockBookingService : IBookingService
    {
        private static readonly Airport[] Airports =
        {
            new Airport("OVB", "Novosibirsk", "Новосибирск", "Россия", "Россия"), 
            new Airport("DME", "Moscow", "Москва", "Россия", "Россия"), 
            new Airport("ATL", "Atlanta", "Атланта", "Соединенные Штаты Америки", "США"), 
            new Airport("DXB", "Dubai", "Дубай", "Объединенные Арабские Эмираты", "ОАЭ"),
        };

        private static readonly String[] TariffsBaggage = { "23 кг", "32 кг", "Только ручная кладь" };

        private Boolean _IsLogged { get; }
        private IReadOnlyList<Reservation> _Reservations { get; }

        public MockBookingService()
        {
            var rng = new Random();

            _IsLogged = (0 == rng.Next(2));

            _Reservations = E.Range(0, rng.Next(1, 2))
                .Select(idx => new Reservation("RSV" + idx.ToString("D4", CultureInfo.InvariantCulture),
                    E.Range(0, rng.Next(1, 4))
                        .Select(idx2 => new Segment(
                            new Flight(rng.Next(0, 10000).ToString()), Airports[rng.Next(0, Airports.Length)], Airports[rng.Next(0, Airports.Length)], TariffsBaggage[rng.Next(0, TariffsBaggage.Length)])).ToArray()))
                .ToArray();
        }

        public Boolean IsLoggedIn()
        {
            return _IsLogged;
        }

        public IReadOnlyList<Reservation> GetReservations()
        {
            //if (false == IsLoggedIn()) { throw new ApplicationException("Not logged in"); }

            return _Reservations;
        }
    }
}