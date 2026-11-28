import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { isAbort } from '../../app/errorMessage'
import { Alert, Badge, Button, EmptyState, Loading, PageHeader, Pagination } from '../../app/ui'
import { useT } from '../../i18n/LanguageContext'
import { formatDateTime } from '../listings/publicTypes'
import { adminMembersErrorMessage } from './errors'
import { formatUntil } from './formatUntil'
import { SuspendForm } from './SuspendForm'
import type { GetReportedMembersResponse, ReportedMemberItem } from './types'
import '../listings/listings.css'
import './admin-members.css'

const PAGE_SIZE = 20

type Action = 'dismiss' | 'lift'

// Admin: members reported by other members (only those with pending reports), the most reported first,
// then the members suspended now without pending reports (a suspension confirms the reports, so they stay
// here only to have their suspension lifted). The Admin dismisses the reports, suspends the member
// (permanently or until a date) or lifts a suspension.
export function AdminMembersPage() {
  const t = useT()
  const [page, setPage] = useState(1)
  const [data, setData] = useState<GetReportedMembersResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState<{ memberId: string; action: Action } | null>(null)
  // The member whose suspend form is open (one at a time).
  const [suspending, setSuspending] = useState<string | null>(null)

  // Bumped after a change, to load the page again.
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetReportedMembersResponse>(`/moderation/members?page=${page}&pageSize=${PAGE_SIZE}`, { signal: controller.signal })
      .then((result) => {
        setData(result)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(adminMembersErrorMessage(err))
      })
    return () => controller.abort()
  }, [page, version])

  // After a member left the list: go back a page if this one is now empty, else load it again.
  function reload() {
    if (data && data.items.length === 1 && page > 1) setPage(page - 1)
    else setVersion((v) => v + 1)
  }

  // Returns nothing: errors are handled inside, so the buttons can just call it.
  function run(member: ReportedMemberItem, action: Action) {
    const question =
      action === 'dismiss'
        ? t('adminMembers.confirmDismiss', { name: member.name })
        : t('adminMembers.confirmLift', { name: member.name })
    if (!window.confirm(question)) return
    setBusy({ memberId: member.memberId, action })
    setError(null)
    setNotice(null)
    const path = action === 'dismiss' ? 'dismiss-reports' : 'lift-suspension'
    api
      .post(`/moderation/members/${member.memberId}/${path}`)
      .then(() => {
        setNotice(action === 'dismiss' ? t('adminMembers.dismissed', { name: member.name }) : t('adminMembers.lifted', { name: member.name }))
        reload()
      })
      .catch((err: unknown) => {
        setError(adminMembersErrorMessage(err))
      })
      .finally(() => {
        setBusy(null)
      })
  }

  function suspended(member: ReportedMemberItem) {
    setSuspending(null)
    setError(null)
    setNotice(t('adminMembers.suspended', { name: member.name }))
    reload()
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1
  const isBusy = (member: ReportedMemberItem, action: Action) => busy?.memberId === member.memberId && busy.action === action
  const reportCountText = (count: number) => {
    if (count === 0) return t('adminMembers.reportNone')
    if (count === 1) return t('adminMembers.reportOne')
    return t('adminMembers.reportMany', { count })
  }

  return (
    <div className="listings-page admin-members">
      <PageHeader eyebrow={t('adminMembers.eyebrow')} title={t('adminMembers.title')} lead={t('adminMembers.lead')} />
      {notice && <Alert tone="success">{notice}</Alert>}
      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text={t('adminMembers.loading')} />}
      {data !== null && data.totalCount === 0 && <EmptyState title={t('adminMembers.emptyTitle')}>{t('adminMembers.emptyText')}</EmptyState>}
      {data !== null && data.totalCount > 0 && (
        <>
          <div className="listings-list">
            {data.items.map((member) => (
              <article key={member.memberId} className="listing-row admin-members__row">
                <div className="admin-members__body">
                  <h2 className="listing-row__title">
                    <Link to={`/members/${member.memberId}`}>{member.name}</Link>
                  </h2>
                  <p className="admin-members__email">{member.email}</p>
                  <div className="listing-row__meta">
                    {member.isSuspended ? (
                      <Badge tone="danger">
                        {member.suspendedUntil
                          ? t('adminMembers.suspendedUntil', { date: formatUntil(member.suspendedUntil) })
                          : t('adminMembers.suspendedPermanent')}
                      </Badge>
                    ) : (
                      <Badge tone="success">{t('adminMembers.active')}</Badge>
                    )}
                    <span>{reportCountText(member.reportCount)}</span>
                    {member.lastReportedAt && <span>{t('adminMembers.last', { date: formatDateTime(member.lastReportedAt) })}</span>}
                    {member.hiddenListingsCount > 0 && (
                      <Badge tone="warning">
                        {member.hiddenListingsCount === 1
                          ? t('adminMembers.hiddenOne')
                          : t('adminMembers.hiddenMany', { count: member.hiddenListingsCount })}
                      </Badge>
                    )}
                  </div>
                  {member.reports.length > 0 && (
                    <ul className="admin-members__reports">
                      {member.reports.map((report) => (
                        <li key={report.id}>
                          <span className="admin-members__report-meta">
                            {formatDateTime(report.createdAt)}
                            {report.conversationId && ` · ${t('adminMembers.fromConversation')}`}
                          </span>
                          <span className="admin-members__reason">{report.reason}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
                <div className="listing-row__actions">
                  {member.reportCount > 0 && (
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => run(member, 'dismiss')}
                      loading={isBusy(member, 'dismiss')}
                      disabled={busy !== null}
                    >
                      {t('adminMembers.dismiss')}
                    </Button>
                  )}
                  {member.isSuspended ? (
                    <Button variant="secondary" size="sm" onClick={() => run(member, 'lift')} loading={isBusy(member, 'lift')} disabled={busy !== null}>
                      {t('adminMembers.lift')}
                    </Button>
                  ) : (
                    suspending !== member.memberId && (
                      <Button variant="danger" size="sm" onClick={() => setSuspending(member.memberId)} disabled={busy !== null}>
                        {t('adminMembers.suspend')}
                      </Button>
                    )
                  )}
                </div>
                {suspending === member.memberId && (
                  <SuspendForm member={member} onDone={() => suspended(member)} onCancel={() => setSuspending(null)} />
                )}
              </article>
            ))}
          </div>
          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
        </>
      )}
    </div>
  )
}
