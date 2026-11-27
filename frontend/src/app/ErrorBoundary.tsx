import { Component, type ErrorInfo, type ReactNode } from 'react'
import { ErrorPage } from './pages/ErrorPage'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  hasError: boolean
}

// Catches an unexpected error while a page renders and shows ErrorPage instead of a blank screen.
// Layout gives it a key per path, so the error is cleared when the user navigates elsewhere.
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false }
  }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true }
  }

  componentDidCatch(error: unknown, info: ErrorInfo) {
    console.error('Unexpected error while rendering a page', error, info.componentStack)
  }

  render() {
    if (this.state.hasError) return <ErrorPage />
    return this.props.children
  }
}
