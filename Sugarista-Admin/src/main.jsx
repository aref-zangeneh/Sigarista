import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './index.css'
import { ensureJalaliDatePickerStarted } from './utils/jalaliDatePickerSetup'
import App from './App.jsx'

// انتخاب تم ذخیره‌شده قبل از اولین رندر، تا صفحه فلش نزند
const storedTheme = localStorage.getItem('sugarista.admin.theme')
if (storedTheme === 'dark' || storedTheme === 'light') {
  document.documentElement.dataset.theme = storedTheme
}

ensureJalaliDatePickerStarted()

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter basename="/admin">
      <App />
    </BrowserRouter>
  </StrictMode>,
)
