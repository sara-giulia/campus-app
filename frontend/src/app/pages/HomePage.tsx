import { useAuth } from '../auth/AuthContext'
import { ArrowRightIcon, ButtonLink, Chip, HandshakeIcon, ShieldIcon, TagIcon } from '../ui'
import './home.css'

// The five categories of the API (Category enum).
const categories = ['Manuale', 'Notițe', 'Electronice', 'Echipament de laborator', 'Altele']

// Why use it, from the user's side.
const highlights = [
  { icon: TagIcon, title: 'Preț corect', text: 'primești o sugestie de preț' },
  { icon: HandshakeIcon, title: 'Rezervare simplă', text: 'un coleg rezervă, tu confirmi' },
  { icon: ShieldIcon, title: 'Doar între studenți', text: 'cumperi și vinzi în campus' },
]

// Temporary home page: a welcome and the login button. The search with filters and
// paging replaces it later (Anca, frontend/src/features/listings/).
export function HomePage() {
  const { user } = useAuth()

  return (
    <div className="home">
      <section className="home__hero">
        <span className="home__eyebrow">Marketplace pentru studenți</span>
        <h1 className="home__title">
          Bine ai venit pe <span className="home__brand">CampusMarket</span>
        </h1>

        {user ? (
          <p className="home__status">
            Ești autentificat ca <strong>{user.email}</strong>
            {user.role === 'Admin' && ' (Admin)'}.
          </p>
        ) : (
          <ButtonLink to="/login" size="lg">
            Intră în cont <ArrowRightIcon size={18} />
          </ButtonLink>
        )}

        <div className="home__categories" aria-label="Categorii">
          {categories.map((c) => (
            <Chip key={c}>{c}</Chip>
          ))}
        </div>
      </section>

      <ul className="home__highlights">
        {highlights.map((h) => (
          <li key={h.title} className="home__highlight">
            <span className="home__highlight-icon">
              <h.icon size={20} />
            </span>
            <div>
              <p className="home__highlight-title">{h.title}</p>
              <p className="home__highlight-text">{h.text}</p>
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}
