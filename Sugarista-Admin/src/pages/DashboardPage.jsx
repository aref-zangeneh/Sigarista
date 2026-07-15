import { useAdmin } from '../context/AdminContext'
import { DashboardOverview } from '../components/DashboardOverview'

export function DashboardPage() {
  const {
    dashboard,
    dashboardLoading,
    dashboardError,
    loading,
    error,
    navigateToTab,
    loadDashboard,
  } = useAdmin()

  return (
      <DashboardOverview
        dashboard={dashboard}
        loading={dashboardLoading}
        error={dashboardError}
        onNavigate={navigateToTab}
        onRefresh={() => loadDashboard()}
      />
  )
}
