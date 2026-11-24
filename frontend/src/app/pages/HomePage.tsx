import { LatestListings } from '../../features/listings/LatestListings'
import { useAuth } from '../auth/AuthContext'
import { ArrowRightIcon, ButtonLink } from '../ui'
import './home.css'

// Four of the API categories, shown as the photo collage of the sketch until listings have photos.
const tiles = [
  { label: 'Manuale', tone: 'blue' },
  { label: 'Electronice', tone: 'yellow' },
  { label: 'Notițe', tone: 'green' },
  { label: 'Echipament de laborator', tone: 'peach' },
] as const

// Landing page from the sketches (hero + collage), then the newest listings.
export function HomePage() {
  const { user } = useAuth()

  return (
    <>
    <section className="home">
      <div className="home__text">
        <h1 className="home__title">Cumperi și vinzi între colegi, direct în campus.</h1>
        <p className="home__lead">Manuale, notițe, electronice și echipament de laborator, de la studenți din facultatea ta.</p>

        <div className="home__actions">
          <ButtonLink to="/listings" size="lg">
            Vezi anunțurile <ArrowRightIcon size={18} />
          </ButtonLink>
          {user ? (
            <ButtonLink to="/listings/new" size="lg" variant="secondary">
              Publică un anunț
            </ButtonLink>
          ) : (
            <ButtonLink to="/login" size="lg" variant="secondary">
              Intră în cont
            </ButtonLink>
          )}
        </div>
      </div>

      <div className="home__collage" aria-hidden="true">
        {tiles.map((t) => (
          <div key={t.label} className={`home__tile home__tile--${t.tone}`}>
            <span>{t.label}</span>
          </div>
        ))}
      </div>
    </section>
    <LatestListings />
    </>
  )
}
