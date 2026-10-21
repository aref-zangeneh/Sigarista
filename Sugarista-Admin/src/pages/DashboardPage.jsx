import { useAdmin } from '../context/AdminContext'
import { DashboardOverview } from '../components/DashboardOverview'
import { PunchWidget } from '../components/PunchWidget'

export function DashboardPage() {
  const {
    dashboard,
    dashboardLoading,
    dashboardError,
    navigateToTab,
    loadDashboard,
  } = useAdmin()

  return (
    <>
      <PunchWidget />
      <DashboardOverview
        dashboard={dashboard}
        loading={dashboardLoading}
        error={dashboardError}
        onNavigate={navigateToTab}
        onRefresh={() => loadDashboard()}
      />
    </>
  )
}
