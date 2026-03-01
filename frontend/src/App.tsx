import { useState, useEffect } from 'react'
import {
  Card,
  CardHeader,
  Title2,
  Title3,
  Body1,
  Caption1,
  Button,
  Spinner,
  MessageBar,
  MessageBarBody,
  ToggleButton,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import {
  ArrowClockwiseRegular,
  WeatherSunnyRegular,
  RocketRegular,
} from '@fluentui/react-icons'

interface WeatherForecast {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string
}

const useStyles = makeStyles({
  container: {
    minHeight: '100vh',
    display: 'flex',
    flexDirection: 'column',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  header: {
    padding: '2.5rem 2rem 1.5rem',
    textAlign: 'center',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: '0.25rem',
  },
  iconWrapper: {
    fontSize: '2.5rem',
    color: tokens.colorBrandForeground1,
    marginBottom: '0.5rem',
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
  },
  main: {
    flex: '1',
    maxWidth: '1200px',
    width: '100%',
    margin: '0 auto',
    padding: '0 2rem 2rem',
  },
  sectionHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: '1rem',
    marginBottom: '1rem',
  },
  headerActions: {
    display: 'flex',
    gap: '0.5rem',
    alignItems: 'center',
  },
  weatherGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
    gap: '0.75rem',
  },
  weatherCard: {
    padding: '1rem',
  },
  tempValue: {
    fontSize: '2rem',
    fontWeight: 700,
    color: tokens.colorBrandForeground1,
    lineHeight: '1',
  },
  tempUnit: {
    color: tokens.colorNeutralForeground3,
    marginLeft: '0.25rem',
  },
  footer: {
    padding: '1.5rem 2rem',
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  link: {
    color: tokens.colorBrandForegroundLink,
    textDecoration: 'none',
    ':hover': {
      textDecoration: 'underline',
    },
  },
  spinnerContainer: {
    display: 'flex',
    justifyContent: 'center',
    padding: '2rem 0',
  },
})

function App() {
  const styles = useStyles()
  const [weatherData, setWeatherData] = useState<WeatherForecast[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [useCelsius, setUseCelsius] = useState(false)

  const fetchWeatherForecast = async () => {
    setLoading(true)
    setError(null)

    try {
      const response = await fetch('/api/weatherforecast')
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }
      const data: WeatherForecast[] = await response.json()
      setWeatherData(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch weather data')
      console.error('Error fetching weather forecast:', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchWeatherForecast()
  }, [])

  const formatDate = (dateString: string) =>
    new Date(dateString).toLocaleDateString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    })

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <RocketRegular className={styles.iconWrapper} />
        <Title2>Fleet</Title2>
        <Caption1 className={styles.subtitle}>
          Orchestrate AI agents to plan, build, and complete software tasks
        </Caption1>
      </header>

      <main className={styles.main}>
        <div className={styles.sectionHeader}>
          <Title3>
            <WeatherSunnyRegular style={{ marginRight: '0.5rem', verticalAlign: 'text-bottom' }} />
            Weather Forecast
          </Title3>
          <div className={styles.headerActions}>
            <ToggleButton
              checked={useCelsius}
              onClick={() => setUseCelsius((v) => !v)}
              size="small"
              appearance="subtle"
            >
              {useCelsius ? '°C' : '°F'}
            </ToggleButton>
            <Button
              icon={<ArrowClockwiseRegular />}
              appearance="subtle"
              onClick={fetchWeatherForecast}
              disabled={loading}
            >
              {loading ? 'Loading…' : 'Refresh'}
            </Button>
          </div>
        </div>

        {error && (
          <MessageBar intent="error" style={{ marginBottom: '1rem' }}>
            <MessageBarBody>{error}</MessageBarBody>
          </MessageBar>
        )}

        {loading && weatherData.length === 0 && (
          <div className={styles.spinnerContainer}>
            <Spinner label="Loading weather data…" />
          </div>
        )}

        {weatherData.length > 0 && (
          <div className={styles.weatherGrid}>
            {weatherData.map((forecast, index) => (
              <Card key={index} className={styles.weatherCard} size="small">
                <CardHeader
                  header={
                    <Body1>
                      <b>{formatDate(forecast.date)}</b>
                    </Body1>
                  }
                  description={<Caption1>{forecast.summary}</Caption1>}
                />
                <div>
                  <span className={styles.tempValue}>
                    {useCelsius ? forecast.temperatureC : forecast.temperatureF}°
                  </span>
                  <span className={styles.tempUnit}>
                    {useCelsius ? 'C' : 'F'}
                  </span>
                </div>
              </Card>
            ))}
          </div>
        )}
      </main>

      <footer className={styles.footer}>
        <Body1>
          Powered by{' '}
          <a
            href="https://learn.microsoft.com/dotnet/aspire"
            target="_blank"
            rel="noopener noreferrer"
            className={styles.link}
          >
            .NET Aspire
          </a>
        </Body1>
      </footer>
    </div>
  )
}

export default App
