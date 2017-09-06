﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TandemBooking.Models;
using TandemBooking.ViewModels.BookingAdmin;

namespace TandemBooking.Services
{
    public class MessageService
    {
        private readonly SmsService _smsService;
        private readonly IMailService _mailService;
        private readonly TandemBookingContext _context;
        private readonly BookingService _bookingService;
        private readonly BookingCoordinatorSettings _bookingCoordinatorSettings;

        public MessageService(SmsService smsService, TandemBookingContext context, BookingService bookingService, BookingCoordinatorSettings bookingCoordinatorSettings, IMailService mailService)
        {
            _smsService = smsService;
            _context = context;
            _bookingService = bookingService;
            _bookingCoordinatorSettings = bookingCoordinatorSettings;
            _mailService = mailService;
        }

        public async Task SendPilotMessage(ApplicationUser user, string subject, string message, Booking booking)
        {
            await _smsService.Send(user.PhoneNumber, message, booking);
        }

        public async Task SendNewBookingMessage(Booking primaryBooking, Booking[] additionalBookings, bool notifyPassenger, bool notifyPilot)
        {
            var allBookings = new[] {primaryBooking}.Union(additionalBookings).ToArray();
            var bookingDateString = primaryBooking.BookingDate.ToString("dd.MM.yyyy");

            //send message to pilot or booking coordinator
            foreach (var booking in allBookings)
            {
                var assignedPilot = booking.AssignedPilot;

                if (assignedPilot != null)
                {
                    _bookingService.AddEvent(booking, null,
                        $"Assigned to {assignedPilot.Name} ({assignedPilot.PhoneNumber.AsPhoneNumber()})");

                    //send message to pilot
                    if (notifyPilot)
                    {
                        if (assignedPilot.SmsNotification)
                        {
                            var message =
                                $"You have a new flight on {bookingDateString}: {booking.PassengerName}, {booking.PassengerEmail}, {booking.PassengerPhone.AsPhoneNumber()}, {booking.Comment}.";
                            await SendPilotMessage(assignedPilot, "New Booking", message, booking);
                        }
                        if (assignedPilot.EmailNotification)
                        {
                            var subject = $"New flight on {bookingDateString}";
                            var message = $@"Hi {assignedPilot.Name},

You have been assigned a new flight:
Date:            {bookingDateString}. 
Passenger Name:  {booking.PassengerName},
Passenger Phone: {booking.PassengerPhone.AsPhoneNumber()},
Passenger Email: {booking.PassengerEmail ?? "not specified"}
Comments:
{booking.Comment}

Booking Url: http://vossatandem.no/BookingAdmin/Details/{booking.Id}

fly safe!
Booking Coordinator
";
                            await _mailService.Send(assignedPilot.Email, subject, message);
                        }
                    }

                    //send message to booking coordinator
                    var bookingCoordinatorMessage =
                        $"New flight on {bookingDateString} assigned to {assignedPilot.Name}, {booking.Comment}";
                    await _smsService.Send(_bookingCoordinatorSettings.PhoneNumber, bookingCoordinatorMessage, booking);
                }
                else
                {
                    _bookingService.AddEvent(booking, null,
                        $"No pilots available, sent message to tandem coordinator {_bookingCoordinatorSettings.Name} ({_bookingCoordinatorSettings.PhoneNumber.AsPhoneNumber()})");

                    var message =
                        $"Please find a pilot on {bookingDateString}: {booking.PassengerName}, {booking.PassengerEmail}, {booking.PassengerPhone.AsPhoneNumber()}, {booking.Comment}";
                    await _smsService.Send(_bookingCoordinatorSettings.PhoneNumber, message, booking);
                }
            }

            //send message to passenger
            if (notifyPassenger)
            {
                var passengerMessage = "";
                if (allBookings.All(b => b.AssignedPilot != null))
                {
                    var assignedPilot = primaryBooking.AssignedPilot;
                    if (additionalBookings.Any())
                    {
                        passengerMessage =
                            $"Awesome! Your {allBookings.Length} tandem flights on {bookingDateString} are confirmed. You will be contacted by {assignedPilot.Name} ({assignedPilot.PhoneNumber.AsPhoneNumber()}) to coordinate your flights";
                    }
                    else
                    {
                        passengerMessage =
                            $"Awesome! Your tandem flight on {bookingDateString} is confirmed. You will be contacted by {assignedPilot.Name} ({assignedPilot.PhoneNumber.AsPhoneNumber()}) to coordinate the details.";
                    }
                }
                else
                {
                    if (additionalBookings.Any())
                    {
                        passengerMessage =
                            $"Awesome! We will try to find {allBookings.Length} pilots who can take you flying on {bookingDateString}. You will be contacted shortly.";

                    }
                    else
                    {
                        passengerMessage =
                            $"Awesome! We will try to find a pilot who can take you flying on {bookingDateString}. You will be contacted shortly.";
                    }
                }

                await _smsService.Send(primaryBooking.PassengerPhone, passengerMessage, primaryBooking);
                _bookingService.AddEvent(primaryBooking, null, $"Sent confirmation message to {primaryBooking.PassengerName} ({primaryBooking.PassengerPhone.AsPhoneNumber()})");
            }

            _context.SaveChanges();
        }

        public async Task SendMissingPilotMessage(string bookingDateString, Booking booking)
        {
            var message = $"Please find a pilot on {bookingDateString}: {booking.PassengerName}, {booking.PassengerEmail}, {booking.PassengerPhone.AsPhoneNumber()}, {booking.Comment}";
            await _smsService.Send(_bookingCoordinatorSettings.PhoneNumber, message, booking);

            var passengerMessage = $"We're working on finding a pilot for your flight on {bookingDateString}. You will be contacted shortly. If you have any questions, you can contact the tandem booking coordinator, {_bookingCoordinatorSettings.Name} on ({_bookingCoordinatorSettings.PhoneNumber.AsPhoneNumber()})";
            await _smsService.Send(booking.PassengerPhone, passengerMessage, booking);
        }

        public async Task SendNewPilotMessage(string bookingDateString, Booking booking, ApplicationUser previousPilot, bool notifyPassenger)
        {
            //send message to new pilot
            var assignedPilot = booking.AssignedPilot;
            if (assignedPilot.SmsNotification)
            {
                var message = $"You have a new flight on {bookingDateString}: {booking.PassengerName}, {booking.PassengerEmail}, {booking.PassengerPhone.AsPhoneNumber()}, {booking.Comment}.";
                await SendPilotMessage(assignedPilot, "Assigned Booking", message, booking);
            }
            if (assignedPilot.EmailNotification)
            {
                var subject = $"New flight on {bookingDateString}";
                var message = $@"Hi {assignedPilot.Name},

You have been assigned a flight:
Date:            {bookingDateString}. 
Passenger Name:  {booking.PassengerName},
Passenger Phone: {booking.PassengerPhone.AsPhoneNumber()},
Passenger Email: {booking.PassengerEmail ?? "not specified"}
Comments:
{booking.Comment}

Booking Url: http://vossatandem.no/BookingAdmin/Details/{booking.Id}

fly safe!
Booking Coordinator
";
                await _mailService.Send(assignedPilot.Email, subject, message);
            }

            //send message to passenger
            if (notifyPassenger)
            {
                var passengerMessage = $"Your flight on {bookingDateString} has been assigned a new pilot. You will be contacted by {assignedPilot.Name} ({assignedPilot.PhoneNumber.AsPhoneNumber()}) shortly.";
                await _smsService.Send(booking.PassengerPhone, passengerMessage, booking);
            }
        }

        public async Task SendPilotUnassignedMessage(Booking booking, ApplicationUser previousPilot)
        {
            var bookingDateString = booking.BookingDate.ToString("dd.MM.yyyy");

            if (previousPilot.SmsNotification)
            {
                var message = $"Your booking on {bookingDateString} has been reassigned to another pilot";
                await SendPilotMessage(previousPilot, "Booking reassigned", message, booking);
            }
            if (previousPilot.EmailNotification)
            {
                var message =
                    $@"Hi {previousPilot.Name},

Your flight on {bookingDateString} has been assigned another pilot.

Booking Url: http://vossatandem.no/BookingAdmin/Details/{booking
                        .Id}

fly safe!
Booking Coordinator
";
                await _mailService.Send(previousPilot.Email, $"Booking on {bookingDateString} reassigned", message);
            }
        }

        public async Task SendCancelMessage(string cancelMessage, Booking booking)
        {
            var message = $"Unfortunately, your flight on {booking.BookingDate.ToString("dd.MM.yyyy")} has been canceled due to {cancelMessage}";
            await _smsService.Send(booking.PassengerPhone, message, booking);
        }

        public async Task SendPassengerMessage(SendMessageViewModel input, Booking booking)
        {
            await _smsService.Send(booking.PassengerPhone, input.EventMessage, booking);
        }

    }
}
