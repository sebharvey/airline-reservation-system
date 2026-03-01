# System Architecture - Design

## Overview

This outlines the design for an airline reservation system based on offer and order capability (Modern Airline Retailing).

The system will ahve the following core concepts.

- Offer - returngs availability and pricing of the airlines flights
- Order - creates orders (bookings on the plane) based on the offer, with passenger information included, and takes payment
- Payment - payment orchestration, supporting at first credit card payments but in future other payment methods like PayPal and ApplePay.
- Servicing - chagne and cancel of orders
- Delivery - Akin to departure control, including online check in (OLCI), irregular operations (IROPS), seat allocation, gate management

## High level system architecture

