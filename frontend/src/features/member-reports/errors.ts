import { ApiError } from '../../api/errors'
import { errorMessage } from '../../app/errorMessage'
import { translate } from '../../i18n/store'

// The error codes of POST /api/v1/members/{id}/reports (ReportMemberHandler, MemberReport in the domain).
export function reportMemberErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return errorMessage(error)
  switch (error.code) {
    case 'MemberReport.ReasonEmpty':
      return translate('memberReports.error.ReasonEmpty')
    case 'MemberReport.ReasonTooLong':
      return translate('memberReports.error.ReasonTooLong', { max: REPORT_REASON_MAX })
    case 'MemberReport.Self':
      return translate('memberReports.error.Self')
    case 'MemberReport.ConversationInvalid':
      return translate('memberReports.error.ConversationInvalid')
    case 'MemberReport.AlreadyExists':
      return translate('memberReports.error.AlreadyExists')
    case 'Member.NotFound':
      return translate('memberReports.error.MemberNotFound')
    default:
      return errorMessage(error)
  }
}

// Report.MaxReasonLength in the domain.
export const REPORT_REASON_MAX = 500
