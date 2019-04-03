using System;
using System.Collections.Generic;

namespace TandemBooking.Models
{
    public enum FlightType
    {
        Other = 0,
        Gondola = 1,
        Winch = 2
    }

    public class Booking {
        public Guid Id {get;set;}
        public DateTime DateRegistered {get;set;}
        public DateTime BookingDate {get;set;}
        public bool Canceled { get; set; }
        public bool Completed { get; set; }
        public decimal PassengerFee { get; set; }
        public int? PassengerWeight { get; set; }

        public string PassengerName {get;set;}
        public string PassengerEmail {get;set;}
        public string PassengerPhone {get;set;} 
        public string Comment {get;set;}
        public FlightType? FlightType { get; set; }
        public string BoatDriver { get; set; }

        public string AssignedPilotId { get; set; }
        public ApplicationUser AssignedPilot { get; set; }

        public Booking PrimaryBooking { get; set; }
        public ICollection<Booking> AdditionalBookings { get; set; }

        public ICollection<BookedPilot> BookedPilots { get; set; }
        public ICollection<BookingEvent> BookingEvents { get; set; }
    }
}