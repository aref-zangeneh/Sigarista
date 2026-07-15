import { useAdmin } from '../context/AdminContext'
import { InventoryPurchasesPanel } from '../components/InventoryPurchasesPanel'
import { UNIT_OPTIONS } from '../constants/adminForms'

export function InventoryPurchasesPage() {
  const {
    session,
    ingredientOptions,
    purchaseExpiringOnly,
    setPurchaseExpiringOnly,
    tableReloadToken,
    saving,
    setSaving,
    setMessage,
    setError,
    bumpTableReload,
    apiRequest,
    getReadableError,
  } = useAdmin()

  return (
      <InventoryPurchasesPanel
        session={session}
        ingredientOptions={ingredientOptions}
        unitOptions={UNIT_OPTIONS}
        apiRequest={apiRequest}
        getReadableError={getReadableError}
        tableReloadToken={tableReloadToken}
        bumpTableReload={bumpTableReload}
        setMessage={setMessage}
        setError={setError}
        saving={saving}
        setSaving={setSaving}
        showExpiringOnly={purchaseExpiringOnly}
        onShowExpiringOnlyChange={setPurchaseExpiringOnly}
      />
  )
}
