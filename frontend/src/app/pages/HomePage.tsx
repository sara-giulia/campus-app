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

// Landing page from the sketches (hero + collage). The listings grid ("Explorează") comes
// with Anca's listings task, in frontend/src/features/listings/.
export function HomePage() {
  const { user } = useAuth()

  return (
    <section className="home">
      <div className="home__text">
        <h1 className="home__title">Cumperi și vinzi între colegi, direct în campus.</h1>
        <p className="home__lead">Manuale, notițe, electronice și echipament de laborator, de la studenți din facultatea ta.</p>

        {user ? (
          <p className="home__status">
            Ești autentificat ca <strong>{user.email}</strong>.
          </p>
        ) : (
          <ButtonLink to="/login" size="lg">
            Intră în cont <ArrowRightIcon size={18} />
          </ButtonLink>
        )}
      </div>

      <div className="home__collage" aria-hidden="true">
        {tiles.map((t) => (
          <div key={t.label} className={`home__tile home__tile--${t.tone}`}>
            <span>{t.label}</span>
          </div>
        ))}
      </div>
    </section>
  )
}
