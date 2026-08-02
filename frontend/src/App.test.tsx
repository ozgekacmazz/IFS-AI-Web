import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('application shell', () => {
  it('explains that summarization is not implemented yet', () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: 'Uygulama temeli hazır' })).toBeVisible()
    expect(screen.getByText('Özetleme özelliği henüz uygulanmadı.')).toBeVisible()
  })
})
