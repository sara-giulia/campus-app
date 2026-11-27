import { ApiError } from '../../api/errors'
import { errorMessage } from '../../app/errorMessage'
import { translate } from '../../i18n/store'

// Member.MaxSuspensionReasonLength and Member.MaxSuspensionLength in the domain.
export const SUSPENSION_REASON_MAX = 500
export const SUSPENSION_MAX_DAYS = 365

// Text for the error codes of the /moderation/members endpoints (list, dismiss-reports, suspend,
// lift-suspension). Anything else goes to the shared errorMessage.
export function adminMembersErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return errorMessage(error)
  switch (error.code) {
    case 'Suspension.ReasonEmpty':
      return translate('adminMembers.error.ReasonEmpty')
    case 'Suspension.ReasonTooLong':
      return translate('adminMembers.error.ReasonTooLong', { max: SUSPENSION_REASON_MAX })
    case 'Suspension.UntilInPast':
      return translate('adminMembers.error.UntilInPast')
    case 'Suspension.TooLong':
      return translate('adminMembers.error.TooLong', { days: SUSPENSION_MAX_DAYS })
    case 'Suspension.Self':
      return translate('adminMembers.error.Self')
    case 'Member.CannotSuspendAdmin':
      return translate('adminMembers.error.CannotSuspendAdmin')
    case 'Member.AlreadySuspended':
      return translate('adminMembers.error.AlreadySuspended')
    case 'Member.NotSuspended':
      return translate('adminMembers.error.NotSuspended')
    case 'Member.NotFound':
      return translate('adminMembers.error.MemberNotFound')
    case 'Report.NoPendingReports':
      return translate('adminMembers.error.NoPendingReports')
    case 'Moderation.InvalidPage':
    case 'Moderation.InvalidPageSize':
      return translate('adminMembers.error.InvalidPage')
    default:
      return errorMessage(error)
  }
}

// Which field of the suspend form an error belongs to (null = shown under the whole form).
export function suspendErrorField(error: unknown): 'reason' | 'until' | null {
  if (!(error instanceof ApiError)) return null
  switch (error.code) {
    case 'Suspension.ReasonEmpty':
    case 'Suspension.ReasonTooLong':
      return 'reason'
    case 'Suspension.UntilInPast':
    case 'Suspension.TooLong':
      return 'until'
    default:
      return null
  }
}
