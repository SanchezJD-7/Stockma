import { type MouseEvent } from 'react'
import '../../landing.css'
import shared from '../../landing.module.css'

import { Comparison } from '../Comparison/Comparison'
import { DemoSection } from '../DemoSection/DemoSection'
import { Faq } from '../Faq/Faq'
import { Features } from '../Features/Features'
import { Footer } from '../Footer/Footer'
import { Header } from '../Header/Header'
import { Hero } from '../Hero/Hero'
import { HowItWorks } from '../HowItWorks/HowItWorks'
import { Pricing } from '../Pricing/Pricing'
import { WaitlistSection } from '../WaitlistSection/WaitlistSection'
import { WhoItsFor } from '../WhoItsFor/WhoItsFor'

export function LandingPage() {
  function handleInternalNavigation(event: MouseEvent<HTMLDivElement>) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return
    }

    const clickedElement = event.target
    if (!(clickedElement instanceof Element)) return

    const anchor = clickedElement.closest<HTMLAnchorElement>('a[href^="#"]')
    const targetId = anchor?.getAttribute('href')?.slice(1)
    if (!targetId) return

    const target = document.getElementById(targetId)
    if (!target) return

    event.preventDefault()

    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
    target.scrollIntoView({ behavior: reduceMotion ? 'instant' : 'smooth', block: 'start' })
  }

  return (
    <div id='landing-page' className={shared.page} onClick={handleInternalNavigation}>
      <Header />
      <main>
        <Hero />
        <WhoItsFor />
        <HowItWorks />
        <Features />
        <Comparison />
        <Pricing />
        <Faq />
        <DemoSection />
        <WaitlistSection />
      </main>
      <Footer />
    </div>
  )
}
