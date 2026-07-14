import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import { ensureJalaliDatePickerStarted } from './utils/jalaliDatePickerSetup'
import App from './App.jsx'

ensureJalaliDatePickerStarted()

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
