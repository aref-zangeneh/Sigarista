import { useAdmin } from '../context/AdminContext'
import { ProductWikiPanel } from '../components/ProductWikiPanel'

export function ProductWikiPage() {
  const {
    session,
    menuItemOptions,
    ingredientOptions,
    saving,
    setSaving,
    setMessage,
    setError,
    apiRequest,
    getReadableError,
  } = useAdmin()

  return (
      <ProductWikiPanel
        session={session}
        menuItemOptions={menuItemOptions}
        ingredientOptions={ingredientOptions}
        apiRequest={apiRequest}
        getReadableError={getReadableError}
        setMessage={setMessage}
        setError={setError}
        saving={saving}
        setSaving={setSaving}
      />
  )
}
