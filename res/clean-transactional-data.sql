-- =============================================================================
-- APEX AIR — CLEAN TRANSACTIONAL DATA
-- =============================================================================
-- Removes all transactional/operational data so the database is ready for a
-- fresh set of bookings.
--
-- PRESERVED (untouched):
--   offer.FareRule           — fare pricing rules
--   order.SsrCatalogue       — SSR reference catalogue
--   offer.Fare               — per-flight fare data (regenerated on inventory import)
--   offer.FlightInventory    — flight inventory (regenerated on schedule import)
--   schedule.*               — schedule groups and flight schedules
--   seat.*                   — aircraft types, seatmaps, seat pricing
--   bag.*                    — bag policy and pricing
--   product.*                — product groups, products, product prices
--   customer.Customer        — customer accounts
--   customer.TierConfig      — loyalty tier configuration
--   customer.Preferences     — customer preferences
--   identity.UserAccount     — web/app user accounts
--   user.User                — staff/employee accounts
--
-- REMOVED (transactional data):
--   delivery.Document        — ancillary documents
--   delivery.Manifest        — check-in manifests
--   delivery.Ticket          — e-tickets (IDENTITY counter reset to 1000000001)
--   payment.PaymentEvent     — payment audit events
--   payment.Payment          — payment records
--   customer.Order           — customer–order linkage
--   customer.LoyaltyTransaction — loyalty points history
--   offer.InventoryHold      — in-flight inventory holds
--   offer.StoredOffer        — stored offer snapshots
--   order.Order              — confirmed/draft orders
--   order.Basket             — active and expired baskets
--   disruption.DisruptionEvent — disruption log
--   identity.RefreshToken    — active login sessions
--
-- IMPORTANT: offer.Fare and offer.FlightInventory are intentionally left intact
-- so that existing fare data and inventory remain queryable. Run the inventory
-- import job after clearing orders if you want a full inventory reset too.
-- =============================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

PRINT 'Starting transactional data cleanup...';

-- -----------------------------------------------------------------------------
-- 1. delivery.Document
--    No FK references this table. Safe to delete first.
-- -----------------------------------------------------------------------------

DELETE FROM [delivery].[Document];
PRINT CONCAT('  delivery.Document:         ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 2. delivery.Manifest
--    FK: delivery.Manifest → delivery.Ticket  (must delete before Ticket)
-- -----------------------------------------------------------------------------

DELETE FROM [delivery].[Manifest];
PRINT CONCAT('  delivery.Manifest:         ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 3. delivery.Ticket
--    FK: referenced by delivery.Manifest (now empty). Safe to delete.
--    IDENTITY is reset after delete so ticket numbers restart at 1000000001.
-- -----------------------------------------------------------------------------

DELETE FROM [delivery].[Ticket];
PRINT CONCAT('  delivery.Ticket:           ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 4. payment.PaymentEvent
--    FK: payment.PaymentEvent → payment.Payment  (must delete before Payment)
-- -----------------------------------------------------------------------------

DELETE FROM [payment].[PaymentEvent];
PRINT CONCAT('  payment.PaymentEvent:      ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 5. payment.Payment
--    FK: referenced by payment.PaymentEvent (now empty). Safe to delete.
--    BookingReference is a plain VARCHAR — no FK to order.Order.
-- -----------------------------------------------------------------------------

DELETE FROM [payment].[Payment];
PRINT CONCAT('  payment.Payment:           ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 6. customer.LoyaltyTransaction
--    FK: customer.LoyaltyTransaction → customer.Customer  (Customer is kept)
-- -----------------------------------------------------------------------------

DELETE FROM [customer].[LoyaltyTransaction];
PRINT CONCAT('  customer.LoyaltyTransaction:', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 7. customer.Order
--    FK: customer.Order → customer.Customer  (Customer is kept)
-- -----------------------------------------------------------------------------

DELETE FROM [customer].[Order];
PRINT CONCAT('  customer.Order:            ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 8. offer.InventoryHold
--    FK: offer.InventoryHold → offer.FlightInventory  (must delete before FlightInventory)
-- -----------------------------------------------------------------------------

DELETE FROM [offer].[InventoryHold];
PRINT CONCAT('  offer.InventoryHold:       ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 9. offer.StoredOffer
--    No FK constraints. Snapshots of priced offers from search sessions.
-- -----------------------------------------------------------------------------

DELETE FROM [offer].[StoredOffer];
PRINT CONCAT('  offer.StoredOffer:         ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 10. order.Order
--     No FK constraints defined. BookingReference and other fields are plain
--     columns — delivery.Ticket and payment.Payment link by value, not FK.
-- -----------------------------------------------------------------------------

DELETE FROM [order].[Order];
PRINT CONCAT('  order.Order:               ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 11. order.Basket
--     No FK constraints defined. ConfirmedOrderId is a plain column.
-- -----------------------------------------------------------------------------

DELETE FROM [order].[Basket];
PRINT CONCAT('  order.Basket:              ', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 12. disruption.DisruptionEvent
--     No FK constraints.
-- -----------------------------------------------------------------------------

DELETE FROM [disruption].[DisruptionEvent];
PRINT CONCAT('  disruption.DisruptionEvent:', @@ROWCOUNT, ' rows removed.');

-- -----------------------------------------------------------------------------
-- 13. identity.RefreshToken
--     FK: identity.RefreshToken → identity.UserAccount  (UserAccount is kept)
--     Clearing tokens forces all users to re-authenticate — clean session state.
-- -----------------------------------------------------------------------------

DELETE FROM [identity].[RefreshToken];
PRINT CONCAT('  identity.RefreshToken:     ', @@ROWCOUNT, ' rows removed.');

COMMIT TRANSACTION;

-- -----------------------------------------------------------------------------
-- Reset the e-ticket IDENTITY sequence so numbers restart at 1000000001.
-- Must run outside the transaction as DBCC CHECKIDENT cannot be used inside
-- an explicit transaction that also modifies the table.
-- -----------------------------------------------------------------------------

DBCC CHECKIDENT ('[delivery].[Ticket]', RESEED, 1000000000);

PRINT '';
PRINT 'Cleanup complete. E-ticket sequence reseeded to 1000000001.';
PRINT '';
PRINT 'Preserved: offer.FareRule, offer.Fare, offer.FlightInventory,';
PRINT '           schedule.*, seat.*, bag.*, product.*, order.SsrCatalogue,';
PRINT '           customer.Customer, customer.TierConfig, customer.Preferences,';
PRINT '           identity.UserAccount, user.User';
