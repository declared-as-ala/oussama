ALTER TABLE "Organizations"
ADD COLUMN IF NOT EXISTS "SubscriptionDaysRemaining" INTEGER NOT NULL DEFAULT 30,
ADD COLUMN IF NOT EXISTS "SubscriptionMonitorEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
ADD COLUMN IF NOT EXISTS "LastSubscriptionDecrementAt" TIMESTAMP NULL,
ADD COLUMN IF NOT EXISTS "SubscriptionExpiryAlertSent" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS idx_org_subscription_days ON "Organizations"("SubscriptionDaysRemaining");
CREATE INDEX IF NOT EXISTS idx_org_subscription_monitor_enabled ON "Organizations"("SubscriptionMonitorEnabled");
CREATE INDEX IF NOT EXISTS idx_org_subscription_status_days ON "Organizations"("Status", "SubscriptionDaysRemaining");
