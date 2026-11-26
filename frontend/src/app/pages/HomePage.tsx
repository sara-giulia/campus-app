import { LatestListings } from '../../features/listings/LatestListings'
import { useT } from '../../i18n/LanguageContext'
import type { TKey } from '../../i18n/ro'
import { useAuth } from '../auth/AuthContext'
import { ArrowRightIcon, ButtonLink } from '../ui'
import './home.css'

// Four of the API categories, shown as the photo collage of the sketch until listings have photos.
const tiles = [
  { label: 'home.tile.books', tone: 'blue' },
  { label: 'home.tile.electronics', tone: 'yellow' },
  { label: 'home.tile.notes', tone: 'green' },
  { label: 'home.tile.lab', tone: 'peach' },
] as const satisfies { label: TKey; tone: string }[]

// Landing page from the sketches (hero + collage), then the newest listings.
export function HomePage() {
  const { user } = useAuth()
  const t = useT()

  return (
    <>
    <section className="home">
      <div className="home__text">
        <h1 className="home__title">{t('home.title')}</h1>
        <p className="home__lead">{t('home.lead')}</p>

        <div className="home__actions">
          <ButtonLink to="/listings" size="lg">
            {t('home.seeListings')} <ArrowRightIcon size={18} />
          </ButtonLink>
          {user ? (
            <ButtonLink to="/listings/new" size="lg" variant="secondary">
              {t('home.publish')}
            </ButtonLink>
          ) : (
            <ButtonLink to="/login" size="lg" variant="secondary">
              {t('layout.login')}
            </ButtonLink>
          )}
        </div>
      </div>

      <div className="home__collage" aria-hidden="true">
        {tiles.map((tile) => (
          <div key={tile.label} className={`home__tile home__tile--${tile.tone}`}>
            <span>{t(tile.label)}</span>
          </div>
        ))}
      </div>
    </section>
    <LatestListings />
    </>
  )
}
