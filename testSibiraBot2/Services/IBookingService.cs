using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace testSibiraBot2.Services
{
    public interface IBookingService
    {
        bool IsLoggedIn();

        IReadOnlyList<Reservation> GetReservations();
    }

    [Serializable]
    public class Reservation
    {
        public String Id { get; private set; }

        public IReadOnlyList<Segment> Segments { get; private set; }

        public Reservation(String id, IReadOnlyList<Segment> segments)
        {
            Id = id;
            Segments = new List<Segment>(segments);
        }
    }

    [Serializable]
    public class Segment
    {
        public Flight Flight { get; private set; }
        public Airport From { get; private set; }
        public Airport To { get; private set; }

        public String TariffBaggage { get; private set; }

        public Segment(Flight flight, Airport @from, Airport to, String tariffBaggage)
        {
            Flight = flight;
            From = @from;
            To = to;
            TariffBaggage = tariffBaggage;
        }
    }

    [Serializable]
    public class Flight
    {
        public String Code { get; private set; }

        public Flight(String code)
        {
            Code = code;
        }
    }

    [Serializable]
    public class Airport
    {
        public String Code { get; private set; }
        public String City { get; private set; }
        public String CityRu { get; private set; }
        public String Country { get; private set; }
        public String CountryRu { get; private set; }

        public Airport(String code, String city, String cityRu, String country, String countryRu)
        {
            Code = code;
            City = city;
            CityRu = cityRu;
            Country = country;
            CountryRu = countryRu;
        }
    }
}