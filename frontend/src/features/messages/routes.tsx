import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { ConversationPage } from './ConversationPage'
import { MessagesPage } from './MessagesPage'

export const messagesRoutes: RouteObject[] = [
  { path: 'messages', element: <RequireAuth><MessagesPage /></RequireAuth> },
  { path: 'messages/:id', element: <RequireAuth><ConversationPage /></RequireAuth> },
]
