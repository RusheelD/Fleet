import { useState, useCallback } from 'react'
import {
    makeStyles,
    tokens,
    Title1,
    Title2,
    Title3,
    Body1,
    Body2,
    Button,
    Card,
    Input,
    Textarea,
    Field,
    MessageBar,
    MessageBarBody,
} from '@fluentui/react-components'
import {
    MailRegular,
    ChatRegular,
    BuildingRegular,
} from '@fluentui/react-icons'

const useStyles = makeStyles({
    hero: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        paddingTop: '80px',
        paddingBottom: '48px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        background: `linear-gradient(180deg, ${tokens.colorNeutralBackground1} 0%, ${tokens.colorNeutralBackground3} 100%)`,
        '@media (max-width: 900px)': {
            paddingTop: '64px',
            paddingBottom: '40px',
            paddingLeft: tokens.spacingHorizontalM,
            paddingRight: tokens.spacingHorizontalM,
        },
    },
    heroSubtitle: {
        maxWidth: '560px',
        marginTop: tokens.spacingVerticalM,
        color: tokens.colorNeutralForeground2,
    },
    section: {
        paddingTop: '48px',
        paddingBottom: '80px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        '@media (max-width: 900px)': {
            paddingTop: '40px',
            paddingBottom: '56px',
            paddingLeft: tokens.spacingHorizontalM,
            paddingRight: tokens.spacingHorizontalM,
        },
    },
    grid: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: tokens.spacingHorizontalXXXL,
        maxWidth: '1000px',
        marginLeft: 'auto',
        marginRight: 'auto',
        '@media (max-width: 768px)': {
            gridTemplateColumns: '1fr',
        },
    },
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        minWidth: 0,
    },
    sidebar: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
        minWidth: 0,
    },
    contactCard: {
        padding: tokens.spacingVerticalL,
    },
    contactIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
    },
    inputControl: {
        width: '100%',
        minWidth: 0,
    },
    submitButton: {
        '@media (max-width: 600px)': {
            width: '100%',
        },
    },
})

export function ContactPage() {
    const styles = useStyles()
    const [submitted, setSubmitted] = useState(false)
    const [name, setName] = useState('')
    const [email, setEmail] = useState('')
    const [subject, setSubject] = useState('')
    const [message, setMessage] = useState('')

    const handleSubmit = useCallback((e: React.FormEvent) => {
        e.preventDefault()
        // In production this would POST to an API endpoint
        setSubmitted(true)
        setName('')
        setEmail('')
        setSubject('')
        setMessage('')
    }, [])

    return (
        <>
            {/* Hero */}
            <section className={styles.hero}>
                <Title1 as="h1">Contact us</Title1>
                <Body1 className={styles.heroSubtitle} as="p">
                    Have questions about Fleet? Want to discuss enterprise pricing?
                    We&apos;d love to hear from you.
                </Body1>
            </section>

            {/* Form + Sidebar */}
            <section className={styles.section}>
                <div className={styles.grid}>
                    {/* Form */}
                    <form className={styles.form} onSubmit={handleSubmit}>
                        <Title2 as="h2">Send us a message</Title2>

                        {submitted && (
                            <MessageBar intent="success">
                                <MessageBarBody>
                                    Thanks for reaching out! We&apos;ll get back to you within 24 hours.
                                </MessageBarBody>
                            </MessageBar>
                        )}

                        <Field label="Name" required>
                            <Input
                                value={name}
                                onChange={(_e, data) => setName(data.value)}
                                placeholder="Your name"
                                className={styles.inputControl}
                            />
                        </Field>

                        <Field label="Email" required>
                            <Input
                                type="email"
                                value={email}
                                onChange={(_e, data) => setEmail(data.value)}
                                placeholder="you@example.com"
                                className={styles.inputControl}
                            />
                        </Field>

                        <Field label="Subject">
                            <Input
                                value={subject}
                                onChange={(_e, data) => setSubject(data.value)}
                                placeholder="What is this about?"
                                className={styles.inputControl}
                            />
                        </Field>

                        <Field label="Message" required>
                            <Textarea
                                value={message}
                                onChange={(_e, data) => setMessage(data.value)}
                                placeholder="Tell us how we can help..."
                                rows={6}
                                className={styles.inputControl}
                            />
                        </Field>

                        <Button appearance="primary" size="large" type="submit" className={styles.submitButton}>
                            Send message
                        </Button>
                    </form>

                    {/* Sidebar */}
                    <div className={styles.sidebar}>
                        <Card className={styles.contactCard}>
                            <MailRegular className={styles.contactIcon} />
                            <Title3>Email</Title3>
                            <Body2>hello@fleet-ai.dev</Body2>
                        </Card>

                        <Card className={styles.contactCard}>
                            <ChatRegular className={styles.contactIcon} />
                            <Title3>Support</Title3>
                            <Body2>
                                Pro and Enterprise customers get priority support.
                                Free users can reach us via email.
                            </Body2>
                        </Card>

                        <Card className={styles.contactCard}>
                            <BuildingRegular className={styles.contactIcon} />
                            <Title3>Enterprise</Title3>
                            <Body2>
                                Looking for custom deployment, SSO, or SLAs?
                                Let&apos;s talk about an Enterprise plan.
                            </Body2>
                        </Card>
                    </div>
                </div>
            </section>
        </>
    )
}
