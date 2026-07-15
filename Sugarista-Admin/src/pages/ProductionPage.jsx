import { useAdmin } from '../context/AdminContext'
import { ProductionPanel } from '../components/ProductionPanel'

export function ProductionPage() {
  const {
    session,
    menuItemOptions,
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
      <ProductionPanel
        session={session}
        menuItemOptions={menuItemOptions}
        apiRequest={apiRequest}
        getReadableError={getReadableError}
        tableReloadToken={tableReloadToken}
        bumpTableReload={bumpTableReload}
        setMessage={setMessage}
        setError={setError}
        saving={saving}
        setSaving={setSaving}
      />
  )
}
